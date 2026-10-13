using System.Net.Http.Headers;
using System.Text.Json;

namespace Cazador.Api.Modules.Obtencion.Ocr;

public sealed record TextoOcr(IReadOnlyList<string> Paginas)
{
    public string Texto => string.Join("\n\n", Paginas);
}

// Adaptador interno: solo recibe archivos descargados por el proceso de obtención.
// No expone carga manual de ofertas ni un endpoint público de OCR.
public sealed class OcrSpaceReader(HttpClient client, IConfiguration config)
{
    public const int MaxBytes = 1_000_000;
    public const int MaxPaginas = 3;

    public async Task<TextoOcr> LeerAsync(byte[] archivo, string tipo, CancellationToken ct = default)
    {
        if (archivo.Length == 0 || archivo.Length > MaxBytes)
            throw new InvalidDataException("OCR: archivo vacío o mayor al límite de 1 MB.");
        var formato = tipo.ToLowerInvariant() switch
        {
            "image/png" => ("PNG", "image/png", "promocion.png"),
            "image/jpeg" => ("JPG", "image/jpeg", "promocion.jpg"),
            "application/pdf" => ("PDF", "application/pdf", "promocion.pdf"),
            _ => throw new InvalidDataException("OCR: formato no admitido; use PNG, JPEG o PDF.")
        };
        var key = config["Ocr:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Configure Ocr:ApiKey mediante user-secrets o una variable de entorno.");
        using var body = new MultipartFormDataContent();
        using var file = new ByteArrayContent(archivo);
        file.Headers.ContentType = new MediaTypeHeaderValue(formato.Item2);
        body.Add(file, "file", formato.Item3);
        body.Add(new StringContent(formato.Item1), "filetype");
        body.Add(new StringContent("2"), "OCREngine");
        body.Add(new StringContent("auto"), "language");
        body.Add(new StringContent("false"), "isOverlayRequired");
        body.Add(new StringContent("false"), "isCreateSearchablePdf");
        body.Add(new StringContent("true"), "scale");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ocr.space/parse/image") { Content = body };
        // La clave nunca se incluye en la URL, el texto de una excepción ni los logs.
        request.Headers.Add("apikey", key);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"OCR: el proveedor respondió HTTP {(int)response.StatusCode}.", null, response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > 2_000_000)
                throw new InvalidDataException("OCR: respuesta demasiado grande.");
            buffer.Write(chunk, 0, count);
        }
        return Interpretar(buffer.ToArray());
    }

    public static TextoOcr Interpretar(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("OCRExitCode", out var code) || !EsUno(code)
            || !root.TryGetProperty("IsErroredOnProcessing", out var errored) || errored.ValueKind != JsonValueKind.False
            || !root.TryGetProperty("ParsedResults", out var pages) || pages.ValueKind != JsonValueKind.Array
            || pages.GetArrayLength() is < 1 or > MaxPaginas)
            throw new InvalidDataException("OCR: lectura fallida, parcial o fuera del límite de páginas; no publicar su resultado.");
        var textos = new List<string>();
        foreach (var page in pages.EnumerateArray())
        {
            if (!page.TryGetProperty("FileParseExitCode", out var pageCode) || !EsUno(pageCode)
                || !page.TryGetProperty("ParsedText", out var text) || text.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("OCR: una página no se pudo leer; no publicar un documento incompleto.");
            textos.Add(text.GetString()!.Replace("\r\n", "\n").Replace('\r', '\n').Trim());
        }
        if (textos.All(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("OCR: no se detectó texto; esto no demuestra que no haya una promoción.");
        return new TextoOcr(textos);
    }

    private static bool EsUno(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.TryGetInt32(out var n) && n == 1,
        JsonValueKind.String => value.GetString() == "1",
        _ => false
    };
}
