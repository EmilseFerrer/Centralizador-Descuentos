using System.Net;
using System.Text;
using System.Text.Json;
using Cazador.Api.Modules.Obtencion.Ia;
using Cazador.Api.Modules.Obtencion.Ocr;
using Microsoft.Extensions.Configuration;

const string original = "Inicio\n30% en Combo Mega Cuarto Deluxe.\nVálido únicamente para Take Away.\nOtros cupones\n15%\nInicia sesión";
const string result = """
{"esOferta":true,"requiereRevision":false,"nombre":"Combo Mega Cuarto Deluxe","resumen":"30% de descuento en Combo Mega Cuarto Deluxe.","condiciones":"Válido únicamente para Take Away.","evidenciaCondiciones":"Válido únicamente para Take Away.","desde":null,"hasta":null,"requiereApp":null,"evidenciaApp":null,"requiereSesion":null,"evidenciaSesion":null,"beneficios":[{"tipo":"Porcentaje","descripcion":"30% de descuento","valor":30,"moneda":null,"evidencia":"30% en Combo Mega Cuarto Deluxe."}]}
""";
var parsed = GeminiOfferReader.Validar(result, original);
Check(parsed.Beneficios.Single().Valor == 30 && parsed.RequiereSesion is null && parsed.Hasta is null, "Conservar desconocidos");
foreach (var bad in new[] { result.Replace("\"valor\":30", "\"valor\":50"), result.Replace("\"hasta\":null", "\"hasta\":\"2026-10-31\""), result.Replace("\"Porcentaje\"", "\"Compra\""), result.Replace("\"evidenciaSesion\":null", "\"evidenciaSesion\":\"Texto inventado\"").Replace("\"requiereSesion\":null", "\"requiereSesion\":true") })
{
    try { GeminiOfferReader.Validar(bad, original); throw new Exception("Debía rechazar una interpretación sin respaldo"); }
    catch (InvalidDataException) { }
}
// Un salto de línea introducido por OCR no cambia la evidencia del beneficio.
var multilinea = original.Replace("Mega Cuarto Deluxe.", "Mega Cuarto\r\nDeluxe.").Replace("únicamente para", "únicamente\tpara");
Check(GeminiOfferReader.Validar(result, multilinea).Beneficios.Single().Valor == 30, "Tolerar solo espacios y saltos OCR");
foreach (var alterado in new[] { original.Replace("30%", "20%"), original.Replace("Válido únicamente", "Válido no únicamente"), original.Replace("Deluxe.", "Deluxe!") })
{
    try { GeminiOfferReader.Validar(result, alterado); throw new Exception("Debía rechazar evidencia alterada"); }
    catch (InvalidDataException) { }
}
var oferta = new Cazador.Api.Modules.Obtencion.Readers.OfertaLeida("identidad-fija", "Banco de prueba", original,
    null, null, new[] { new Cazador.Api.Modules.Obtencion.Readers.BeneficioLeido("30% de ahorro", 30) });
MostazaIaPolicy.Validar(oferta, parsed);
foreach (var invalida in new[] { result.Replace("\"requiereRevision\":false", "\"requiereRevision\":true"), result.Replace("\"valor\":30", "\"valor\":20") })
{
    var interpretacion = JsonSerializer.Deserialize<PublicacionIa>(invalida, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    try { MostazaIaPolicy.Validar(oferta, interpretacion); throw new Exception("Debía preservar ficha por interpretación dudosa o porcentaje distinto"); }
    catch (InvalidDataException) { }
}
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Ia:ApiKey"] = "prueba" }).Build();
var handler = new Fake(result);
await new GeminiOfferReader(new HttpClient(handler), config).LeerAsync(original);
Check(handler.Calls == 1, "Una llamada");
handler.Status = HttpStatusCode.TooManyRequests;
try { await new GeminiOfferReader(new HttpClient(handler), config).LeerAsync(original); throw new Exception("Debía rechazar cuota agotada"); }
catch (HttpRequestException ex) { Check(ex.StatusCode == HttpStatusCode.TooManyRequests, "HTTP 429"); }
Console.WriteLine("OK: interpretación estructurada, evidencias, datos desconocidos y errores de cuota verificados.");
if (args.Length == 2 && args[0] == "--imagen")
{
    var secrets = new ConfigurationBuilder().AddUserSecrets(typeof(GeminiOfferReader).Assembly).AddEnvironmentVariables().Build();
    var type = Path.GetExtension(args[1]).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".pdf" => "application/pdf", _ => throw new InvalidDataException("Use PNG, JPEG o PDF") };
    if (new FileInfo(args[1]).Length > OcrSpaceReader.MaxBytes) throw new InvalidDataException("Máximo 1 MB");
    using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(90) };
    try
    {
        var ocr = await new OcrSpaceReader(http, secrets).LeerAsync(await File.ReadAllBytesAsync(args[1]), type);
        var ia = await new GeminiOfferReader(http, secrets).LeerAsync(ocr.Texto);
        Console.WriteLine($"IA real ({ia.Modelo}); resultado de prueba, NO guardado en el catálogo:");
        Console.WriteLine(JsonSerializer.Serialize(ia.Publicacion, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        if (ia.Publicacion.RequiereRevision) Console.WriteLine("Interpretación dudosa: requiere revisión antes de publicar.");
    }
    catch (HttpRequestException ex) { Console.Error.WriteLine($"Proveedor HTTP {(int?)ex.StatusCode}: revise clave, modelo y cuota. No compartir claves."); Environment.ExitCode = 1; }
    catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or JsonException or TaskCanceledException)
    { Console.Error.WriteLine($"Interpretación no completada ({ex.GetType().Name}): {ex.Message}"); Environment.ExitCode = 1; }
}
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
sealed class Fake(string result) : HttpMessageHandler
{
    public int Calls { get; private set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        Check(request.Headers.GetValues("x-goog-api-key").Single() == "prueba" && request.RequestUri!.Query == "", "Clave en encabezado");
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
        Check(body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString() == original, "Texto original intacto");
        var response = JsonSerializer.Serialize(new { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = result } } } } } });
        return new HttpResponseMessage(Status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private const string original = "Inicio\n30% en Combo Mega Cuarto Deluxe.\nVálido únicamente para Take Away.\nOtros cupones\n15%\nInicia sesión";
}
