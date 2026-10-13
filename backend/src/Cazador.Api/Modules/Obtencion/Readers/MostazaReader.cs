using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Cazador.Api.Modules.Obtencion.Readers;

public record BeneficioLeido(string Descripcion, decimal Porcentaje);
public record OfertaLeida(string Identificador, string Nombre, string Condiciones,
    DateOnly? Desde, DateOnly? Hasta, IReadOnlyList<BeneficioLeido> Beneficios);

public static class MostazaReader
{
    public const string Url = "https://www.mostazaweb.com.ar/bases-y-condiciones-promos/";
    public static IReadOnlyList<OfertaLeida> Leer(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var container = doc.DocumentNode.SelectSingleNode("//div[@data-widget_type='text-editor.default']");
        if (container is null) throw new InvalidDataException("Mostaza: no se encontró el bloque de condiciones esperado.");
        var nodes = container.SelectNodes(".//p | .//li");
        if (nodes is null) throw new InvalidDataException("Mostaza: el bloque de condiciones está vacío.");
        var ofertas = new List<OfertaLeida>();
        string? titulo = null;
        var lineas = new List<string>();
        void Cerrar()
        {
            if (titulo is null) return;
            var condiciones = string.Join("\n", lineas);
            var beneficios = lineas.SelectMany(linea => Regex.Matches(linea,
                @"(?<valor>\d+(?:[.,]\d+)?)\s*%\s*de\s*(?:reintegro|ahorro|descuento)", RegexOptions.IgnoreCase)
                .Select(m => new BeneficioLeido(linea, decimal.Parse(m.Groups["valor"].Value.Replace(',', '.'), CultureInfo.InvariantCulture))))
                .Distinct().ToList();
            if (beneficios.Count == 0 || beneficios.Any(b => b.Porcentaje <= 0 || b.Porcentaje > 100))
                throw new InvalidDataException($"Mostaza: beneficio no reconocido para {titulo}; se conserva el catálogo anterior.");
            DateOnly? desde = null, hasta = null;
            var rango = Regex.Match(condiciones, @"del\s+(\d{2}/\d{2}/\d{4})\s+al\s+(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);
            if (rango.Success)
            {
                if (!DateOnly.TryParseExact(rango.Groups[1].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                    || !DateOnly.TryParseExact(rango.Groups[2].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var h) || h < d)
                    throw new InvalidDataException("Mostaza: rango de fechas inválido.");
                desde = d; hasta = h;
            }
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(titulo.ToUpperInvariant())));
            ofertas.Add(new OfertaLeida(key, titulo, condiciones, desde, hasta, beneficios));
        }
        foreach (var node in nodes)
        {
            var texto = Regex.Replace(HtmlEntity.DeEntitize(node.InnerText), @"\s+", " ").Trim();
            if (texto.Length == 0) continue;
            // Los límites se detectan solo en encabezados, nunca en párrafos de condiciones.
            var encabezado = node.Name == "p" && node.SelectSingleNode(".//b | .//strong") is not null
                && (texto.StartsWith("PROMOCIÓN", StringComparison.OrdinalIgnoreCase)
                    || texto.StartsWith("Banco ", StringComparison.OrdinalIgnoreCase)
                    || texto.StartsWith("BASES", StringComparison.OrdinalIgnoreCase));
            if (encabezado)
            {
                Cerrar(); lineas.Clear();
                titulo = texto.Contains("BANCO", StringComparison.OrdinalIgnoreCase)
                    ? Regex.Replace(texto, @"^PROMOCIÓN\s*", "", RegexOptions.IgnoreCase).Trim('«', '»', '“', '”', ' ') : null;
                if (titulo?.Length > 300) throw new InvalidDataException("Mostaza: título demasiado largo.");
            }
            else if (titulo is not null && !texto.Equals("Términos y condiciones", StringComparison.OrdinalIgnoreCase)) lineas.Add(texto);
        }
        Cerrar();
        if (ofertas.Count == 0 || ofertas.Select(o => o.Identificador).Distinct().Count() != ofertas.Count)
            throw new InvalidDataException("Mostaza: no hay ofertas reconocibles o hay encabezados duplicados; se conserva el catálogo anterior.");
        return ofertas;
    }
}
