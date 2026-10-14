using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Cazador.Api.Modules.Obtencion.Ia;
using Cazador.Api.Modules.Obtencion.Ocr;
using Cazador.Api.Modules.Obtencion.Readers;
using HtmlAgilityPack;

namespace Cazador.Api.Modules.Obtencion.Materiales;

public sealed record MaterialPromocional(string Id, Uri Url);
public sealed record MaterialInterpretado(MaterialPromocional Material, LecturaIa Lectura);

public sealed class MostazaMaterialCache
{
    public Dictionary<string, LecturaIa> Lecturas { get; } = new();
}

// Lee exclusivamente recursos promocionales del bloque seleccionado de Mostaza.
public sealed class MostazaMaterialReader(HttpClient http, OcrSpaceReader ocr, GeminiOfferReader ia,
    IConfiguration config, ILogger<MostazaMaterialReader> logger, MostazaMaterialCache sharedCache)
{
    private readonly Dictionary<string, LecturaIa> cache = sharedCache.Lecturas;
    public static IReadOnlyList<MaterialPromocional> Detectar(string html)
    {
        var doc = new HtmlDocument(); doc.LoadHtml(html);
        var block = doc.DocumentNode.SelectSingleNode("//div[@data-widget_type='text-editor.default']");
        if (block is null) throw new InvalidDataException("Mostaza: no se encontró el bloque de promociones para materiales.");
        var result = new Dictionary<string, MaterialPromocional>();
        foreach (var node in block.SelectNodes(".//img | .//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var image = node.Name == "img";
            var label = HtmlEntity.DeEntitize(image ? node.GetAttributeValue("alt", "") + " " + node.GetAttributeValue("title", "") : node.InnerText);
            if (!Regex.IsMatch(label, @"promo|descuento|beneficio|cup[oó]n|reintegro|condiciones", RegexOptions.IgnoreCase)) continue;
            var raw = image ? node.GetAttributeValue("data-src", node.GetAttributeValue("src", "")) : node.GetAttributeValue("href", "");
            if (!Uri.TryCreate(new Uri(MostazaReader.Url), HtmlEntity.DeEntitize(raw), out var uri) || !Permitida(uri)) continue;
            uri = new UriBuilder(uri) { Fragment = "" }.Uri;
            var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (image ? ext is not ".png" and not ".jpg" and not ".jpeg" : ext != ".pdf") continue;
            var id = "material:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
            result.TryAdd(uri.AbsoluteUri, new MaterialPromocional(id, uri));
        }
        if (result.Count > 5) throw new InvalidDataException("Mostaza: más de cinco materiales; revisar fuente antes de procesar.");
        return result.Values.ToList();
    }
    public static bool Permitida(Uri uri) => uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && uri.Host is "www.mostazaweb.com.ar" or "mostazaweb.com.ar";

    public async Task<IReadOnlyList<MaterialInterpretado>> LeerAsync(string html, CancellationToken ct)
    {
        var results = new List<MaterialInterpretado>();
        foreach (var material in Detectar(html))
        {
            try
            {
                var (bytes, type) = await Descargar(material.Url, ct);
                var key = material.Id + ":" + (config["Ia:Modelo"] ?? GeminiOfferReader.ModeloPredeterminado) + ":" + Convert.ToHexString(SHA256.HashData(bytes));
                if (!cache.TryGetValue(key, out var lectura))
                {
                    var texto = await ocr.LeerAsync(bytes, type, ct);
                    lectura = await ia.LeerAsync(texto.Texto, ct);
                    if (!lectura.Publicacion.EsOferta) continue;
                    if (lectura.Publicacion.RequiereRevision) throw new InvalidDataException("Material: interpretación dudosa.");
                    if (cache.Count >= 100) cache.Clear();
                    cache[key] = lectura;
                }
                results.Add(new MaterialInterpretado(material, lectura));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning("Mostaza: no se actualizará el material {Id} ({Tipo}); se conserva su ficha anterior.", material.Id, ex.GetType().Name);
                if (ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } || ex is InvalidOperationException) break;
            }
        }
        logger.LogInformation("Mostaza: {Cantidad} materiales promocionales interpretados por OCR e IA.", results.Count);
        return results;
    }

    public async Task<(byte[] Bytes, string Tipo)> Descargar(Uri url, CancellationToken ct)
    {
        if (!Permitida(url)) throw new InvalidDataException("Material: destino no autorizado.");
        for (var redirect = 0; redirect <= 3; redirect++)
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var target = response.Headers.Location;
                if (target is null || redirect == 3) throw new InvalidDataException("Material: redirección incompleta.");
                url = target.IsAbsoluteUri ? target : new Uri(url, target);
                if (!Permitida(url)) throw new InvalidDataException("Material: redirección fuera de la fuente seleccionada.");
                continue;
            }
            response.EnsureSuccessStatusCode();
            var type = response.Content.Headers.ContentType?.MediaType;
            if (type is not "image/png" and not "image/jpeg" and not "application/pdf") throw new InvalidDataException("Material: tipo de archivo no admitido.");
            if (response.Content.Headers.ContentLength > OcrSpaceReader.MaxBytes) throw new InvalidDataException("Material: supera 1 MB.");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream(); var chunk = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > OcrSpaceReader.MaxBytes) throw new InvalidDataException("Material: supera 1 MB.");
                buffer.Write(chunk, 0, count);
            }
            if (buffer.Length == 0) throw new InvalidDataException("Material: archivo vacío.");
            return (buffer.ToArray(), type);
        }
        throw new InvalidDataException("Material: demasiadas redirecciones.");
    }
}
