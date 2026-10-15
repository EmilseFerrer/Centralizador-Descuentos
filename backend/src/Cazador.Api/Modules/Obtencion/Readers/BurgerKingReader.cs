using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cazador.Api.Modules.Catalogo.Entities;
namespace Cazador.Api.Modules.Obtencion.Readers;

public record BeneficioBk(TipoBeneficio Tipo, string Descripcion, decimal? Valor = null, string? Moneda = null);
public record OfertaBk(string Id, string Nombre, string Resumen, string Condiciones, string Original,
    DateOnly? Desde, DateOnly? Hasta, IReadOnlyList<BeneficioBk> Beneficios);

public static class BurgerKingReader
{
    public const string Url = "https://burgerking.com.ar/es/rewards/offers";
    public const string EngineUrl = "https://use2-prod-bk-gateway.rbictg.com/graphql";
    public const string CmsUrl = "https://czqk28jt.apicdn.sanity.io/v2023-08-01/data/query/prod_bk_ar";
    public const string EngineQuery = "query LoyaltyOffers { loyaltyOffers(omitInvalids:true) { id sanityId startDate endDate serviceModes redemptionType errors { code message } } }";
    public const string CmsQuery = "*[_type == \"systemwideOffer\" && _id in $ids && testOnly != true]{_id,name,description,moreInfo,howToRedeem,mobileOrderOnly,redemptionMethod,shortCode,testOnly,\"imagenUrl\":localizedImage.es.app.asset->url,\"imagenDescripcion\":localizedImage.es.imageDescription}";
    public static JsonElement Activas(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            throw new InvalidDataException("Burger King: error de la consulta pública.");
        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("loyaltyOffers", out var offers) || offers.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Burger King: cambió la respuesta de ofertas.");
        if (offers.GetArrayLength() > 500) throw new InvalidDataException("Burger King: tamaño de catálogo inesperado.");
        return offers.Clone();
    }
    public static IReadOnlyList<OfertaBk> Leer(string engineJson, string cmsJson, DateTimeOffset ahora)
    {
        var activas = Activas(engineJson);
        using var cms = JsonDocument.Parse(cmsJson);
        if (!cms.RootElement.TryGetProperty("result", out var docs) || docs.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Burger King: respuesta de contenido inválida.");
        var byId = docs.EnumerateArray().ToDictionary(x => String(x, "_id"), x => x);
        var result = new List<OfertaBk>();
        foreach (var active in activas.EnumerateArray())
        {
            var id = String(active, "sanityId");
            if (id.Length == 0 || !byId.TryGetValue(id, out var content)) continue;
            if (content.TryGetProperty("testOnly", out var test) && test.ValueKind == JsonValueKind.True) continue;
            if (active.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0) continue;
            var start = Timestamp(active, "startDate"); var end = Timestamp(active, "endDate");
            if (start > ahora || end < ahora) continue;
            // No importar ofertas marcadas exclusivamente para delivery.
            if (active.TryGetProperty("serviceModes", out var modes) && modes.ValueKind == JsonValueKind.Array && modes.GetArrayLength() > 0
                && modes.EnumerateArray().All(x => x.GetString()?.Contains("DELIVERY", StringComparison.OrdinalIgnoreCase) == true)) continue;
            var name = Locale(content, "name");
            var description = Locale(content, "description");
            var more = Locale(content, "moreInfo"); var redeem = Locale(content, "howToRedeem");
            if (Regex.IsMatch(name + " " + more + " " + redeem, @"(?:solo|sólo|exclusiv\w*)\s+(?:para\s+|por\s+|en\s+)?delivery", RegexOptions.IgnoreCase)) continue;
            if (name.Length == 0 || name.Length > 300) throw new InvalidDataException("Burger King: nombre ausente o demasiado largo.");
            var benefits = new List<BeneficioBk>();
            foreach (Match match in Regex.Matches(name, @"(?<n>\d+(?:[.,]\d+)?)\s*%"))
            {
                var value = decimal.Parse(match.Groups["n"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                if (value <= 0 || value > 100) throw new InvalidDataException("Burger King: porcentaje inválido.");
                benefits.Add(new BeneficioBk(TipoBeneficio.Porcentaje, name, value));
            }
            var price = Regex.Match(name, @"\b(?:a|por)\s*\$\s*(?<n>\d+(?:\.\d{3})*(?:,\d{1,2})?)(?![\d.,])", RegexOptions.IgnoreCase);
            if (price.Success)
                benefits.Add(new BeneficioBk(TipoBeneficio.PrecioFinal, name,
                    decimal.Parse(price.Groups["n"].Value, NumberStyles.Number, CultureInfo.GetCultureInfo("es-AR")), "ARS"));
            if (benefits.Count == 0) benefits.Add(new BeneficioBk(TipoBeneficio.Otro, name));
            var code = String(content, "shortCode");
            if (code.Length > 0) benefits.Add(new BeneficioBk(TipoBeneficio.Cupon, "Código publicado: " + code));
            var conditions = new List<string>();
            if (more.Length > 0) conditions.Add(more);
            if (redeem.Length > 0) conditions.Add(redeem);
            var method = String(content, "redemptionMethod");
            if (method == "mobileAndRestaurant") conditions.Add("Canje mediante pedido digital o en restaurante, según las condiciones de la marca.");
            if (method == "mobileOnly") conditions.Add("Canje mediante pedido digital, según las condiciones de la marca.");
            if (method == "restaurantOnly") conditions.Add("Canje en restaurante, según las condiciones de la marca.");
            if (start.HasValue) conditions.Add("Inicio de vigencia: " + start.Value.UtcDateTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            if (end.HasValue && end.Value.Year < 9999) conditions.Add("Fin de vigencia: " + end.Value.UtcDateTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            if (end?.Year == 9999 || end is null) conditions.Add("La fuente no informa una fecha límite utilizable.");
            result.Add(new OfertaBk(id, name, description.Length > 0 ? description : name, string.Join("\n", conditions),
                JsonSerializer.Serialize(new { disponibilidad = active, publicacion = content }),
                start.HasValue ? DateOnly.FromDateTime(start.Value.UtcDateTime) : null,
                end.HasValue && end.Value.Year < 9999 ? DateOnly.FromDateTime(end.Value.UtcDateTime) : null, benefits));
        }
        if (result.Select(x => x.Id).Distinct().Count() != result.Count) throw new InvalidDataException("Burger King: identidades duplicadas.");
        if (activas.GetArrayLength() > 0 && result.Count == 0) throw new InvalidDataException("Burger King: ninguna publicación legible; se conserva el catálogo anterior.");
        return result;
    }
    private static DateTimeOffset? Timestamp(JsonElement e, string key)
    {
        var value = String(e, key);
        if (value.Length == 0) return null;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
            throw new InvalidDataException("Burger King: fecha inválida.");
        return date;
    }
    public static string String(JsonElement e, string key) => e.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static string Locale(JsonElement e, string key)
    {
        if (!e.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("es", out var es)) return "";
        if (es.ValueKind == JsonValueKind.String) return Regex.Replace(es.GetString() ?? "", @"\s+", " ").Trim();
        if (es.ValueKind != JsonValueKind.Array) return "";
        var blocks = new List<string>();
        foreach (var block in es.EnumerateArray())
        {
            if (!block.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array) continue;
            blocks.Add(string.Concat(children.EnumerateArray().Select(child => String(child, "text"))));
        }
        return Regex.Replace(string.Join(" ", blocks), @"\s+", " ").Trim();
    }
}
