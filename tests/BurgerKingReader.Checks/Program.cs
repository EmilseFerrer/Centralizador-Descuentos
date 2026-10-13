using System.Text.Json;
using Cazador.Api.Modules.Catalogo.Entities;
using Cazador.Api.Modules.Obtencion.Readers;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
object Locale(string text) => new { es = new[] { new { children = new[] { new { text } } } } };
object Active(string id, string[]? modes = null, string? end = null) => new { sanityId = id, startDate = "2026-01-01T00:00:00Z", endDate = end ?? "9999-12-31T23:59:59.999Z", serviceModes = modes };
object Cms(string id, string name, bool test = false) => new { _id = id, name = Locale(name), description = Locale("Descripción publicada"), testOnly = test, shortCode = "123", redemptionMethod = "restaurantOnly" };
var engine = JsonSerializer.Serialize(new { data = new { loyaltyOffers = new[] { Active("percent"), Active("price"), Active("delivery", ["DELIVERY"]), Active("expired", end:"2020-01-01T00:00:00Z"), Active("test"), Active("unknown") } } });
var cms = JsonSerializer.Serialize(new { result = new[] { Cms("percent", "Combo 30% OFF"), Cms("price", "Combo a $10.890"), Cms("delivery", "Solo delivery"), Cms("expired", "Vencida"), Cms("test", "Prueba", true), Cms("unknown", "2x1 Papas") } });
var now = new DateTimeOffset(2026,10,13,12,0,0,TimeSpan.Zero);
var result = BurgerKingReader.Leer(engine,cms,now);
Check(result.Count == 3, "Excluir pruebas, vencidas y delivery exclusivo.");
Check(result.Single(x=>x.Id=="percent").Beneficios.Any(x=>x.Tipo==TipoBeneficio.Porcentaje && x.Valor==30), "Extraer porcentaje explícito.");
Check(result.Single(x=>x.Id=="price").Beneficios.Any(x=>x.Tipo==TipoBeneficio.PrecioFinal && x.Valor==10890 && x.Moneda=="ARS"), "Normalizar precio publicado.");
Check(result.All(x=>x.Hasta is null), "No mostrar fecha técnica 9999 como vigencia real.");
Check(result.Single(x=>x.Id=="unknown").Beneficios.Any(x=>x.Tipo==TipoBeneficio.Otro && x.Valor is null), "Conservar otro beneficio sin inventar precio.");
Check(result.All(x=>x.Beneficios.Any(b=>b.Tipo==TipoBeneficio.Cupon)), "Conservar cupón publicado.");
var rejected = false;
try { BurgerKingReader.Activas("{\"errors\":[{\"message\":\"error\"}],\"data\":{\"loyaltyOffers\":null}}"); }
catch (InvalidDataException) { rejected = true; }
Check(rejected,"Errores de API no equivalen a catálogo vacío.");
if (args.Length == 2)
{
    var live = BurgerKingReader.Leer(File.ReadAllText(args[0]),File.ReadAllText(args[1]),now);
    Check(live.Count > 0,"Fuente real legible.");
    Console.WriteLine($"Fuente real: {live.Count} publicaciones, {live.Sum(x=>x.Beneficios.Count)} beneficios; porcentajes {live.Count(x=>x.Beneficios.Any(b=>b.Tipo==TipoBeneficio.Porcentaje))}, precios publicados {live.Count(x=>x.Beneficios.Any(b=>b.Tipo==TipoBeneficio.PrecioFinal))}.");
}
Console.WriteLine("Comprobaciones Burger King aprobadas.");

if (args.Length == 1 && args[0] == "--live")
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
    client.DefaultRequestHeaders.Add("x-ui-region", "AR");
    client.DefaultRequestHeaders.Add("x-ui-language", "es");
    client.DefaultRequestHeaders.Add("x-ui-platform", "web");
    client.DefaultRequestHeaders.Add("apollo-require-preflight", "true");
    var arNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"));
    client.DefaultRequestHeaders.Add("x-user-datetime", arNow.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture));
    var liveEngine = await client.GetStringAsync(BurgerKingReader.EngineUrl + "?query=" + Uri.EscapeDataString(BurgerKingReader.EngineQuery));
    var ids = BurgerKingReader.Activas(liveEngine).EnumerateArray().Select(x=>BurgerKingReader.String(x,"sanityId")).Where(x=>x.Length>0).Distinct().ToArray();
    var liveCms = await client.GetStringAsync(BurgerKingReader.CmsUrl + "?query=" + Uri.EscapeDataString(BurgerKingReader.CmsQuery) + "&%24ids=" + Uri.EscapeDataString(JsonSerializer.Serialize(ids)));
    var fresh = BurgerKingReader.Leer(liveEngine,liveCms,DateTimeOffset.UtcNow);
    Check(fresh.Count>0,"Lectura HTTP desde .NET.");
    Console.WriteLine($"HTTP real desde .NET: {fresh.Count} publicaciones leídas, sin acceso a MySQL.");
}
