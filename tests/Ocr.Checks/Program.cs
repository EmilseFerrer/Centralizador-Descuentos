using System.Net;
using System.Text;
using Cazador.Api.Modules.Obtencion.Ocr;
using Microsoft.Extensions.Configuration;

var correcto = """{"OCRExitCode":1,"IsErroredOnProcessing":false,"ParsedResults":[{"FileParseExitCode":"1","ParsedText":"30% de descuento\r\nSolo con tarjeta"},{"FileParseExitCode":1,"ParsedText":"Hasta 31/10/2026"}]}""";
var texto = OcrSpaceReader.Interpretar(Encoding.UTF8.GetBytes(correcto));
Check(texto.Paginas.Count == 2 && texto.Texto.Contains("30% de descuento\nSolo con tarjeta"), "Conservar páginas, fechas y condiciones");
foreach (var malo in new[] {
    correcto.Replace("\"OCRExitCode\":1", "\"OCRExitCode\":2"),
    correcto.Replace("\"FileParseExitCode\":1", "\"FileParseExitCode\":-20"),
    correcto.Replace("\"IsErroredOnProcessing\":false", "\"IsErroredOnProcessing\":true"),
    """{"OCRExitCode":1,"IsErroredOnProcessing":false,"ParsedResults":[{"FileParseExitCode":1,"ParsedText":" "}]}"""
})
{
    try { OcrSpaceReader.Interpretar(Encoding.UTF8.GetBytes(malo)); throw new Exception("Debía rechazar salida incompleta"); }
    catch (InvalidDataException) { }
}
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Ocr:ApiKey"] = "clave-de-prueba" }).Build();
var handler = new Fake(correcto);
var reader = new OcrSpaceReader(new HttpClient(handler), config);
await reader.LeerAsync([1,2,3], "image/png");
Check(handler.Calls == 1, "Una llamada sin red real");
try { await reader.LeerAsync(new byte[OcrSpaceReader.MaxBytes + 1], "application/pdf"); throw new Exception("Debía rechazar archivo grande"); }
catch (InvalidDataException) { }
Check(handler.Calls == 1, "No consumir API para archivo inválido");
Console.WriteLine("OK: resultados completos, fallos, límites y envío protegido verificados.");

if (args.Length == 2 && args[0] == "--archivo")
{
    var localConfig = new ConfigurationBuilder().AddUserSecrets(typeof(OcrSpaceReader).Assembly).AddEnvironmentVariables().Build();
    var tipo = Path.GetExtension(args[1]).ToLowerInvariant() switch
    { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".pdf" => "application/pdf", _ => throw new InvalidDataException("Use PNG, JPG o PDF") };
    var info = new FileInfo(args[1]);
    if (info.Length > OcrSpaceReader.MaxBytes) throw new InvalidDataException("Máximo 1 MB");
    using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(90) };
    try
    {
        var real = await new OcrSpaceReader(http, localConfig).LeerAsync(await File.ReadAllBytesAsync(args[1]), tipo);
        Console.WriteLine($"OCR real: {real.Paginas.Count} página(s).\n{real.Texto}");
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or InvalidOperationException or TaskCanceledException)
    { Console.Error.WriteLine($"No se completó OCR ({ex.GetType().Name}). Verifique clave, conexión y límites; no compartir la clave."); Environment.ExitCode = 1; }
}
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
sealed class Fake(string json) : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        if (request.Method != HttpMethod.Post || request.RequestUri!.Query != "" || request.Headers.GetValues("apikey").Single() != "clave-de-prueba") throw new Exception("Clave debe estar en encabezado");
        var body = await request.Content!.ReadAsStringAsync(ct);
        if (!body.Contains("promocion.png") || !body.Contains("OCREngine") || body.Contains("clave-de-prueba")) throw new Exception("Multipart inválido");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
