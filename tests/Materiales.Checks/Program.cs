using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Cazador.Api.Modules.Obtencion.Materiales;
using Cazador.Api.Modules.Obtencion.Ocr;
using Cazador.Api.Modules.Obtencion.Ia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

const string html = """
<img alt="Promoción fuera del bloque" src="/fuera.png">
<div data-widget_type="text-editor.default">
<img alt="Logo Mostaza" src="/logo.png">
<img alt="Promoción de comida" src="/oferta.png">
<img alt="Promoción repetida" src="/oferta.png">
<a href="/condiciones.pdf">Condiciones de la promoción</a>
<img alt="Promoción externa" src="https://otro.example/oferta.png">
<img alt="Promoción HTTP" src="http://www.mostazaweb.com.ar/oferta.png">
<img alt="Promoción formato no soportado" src="/oferta.svg">
</div>
""";
var found = MostazaMaterialReader.Detectar(html);
Check(found.Count == 2 && found.Select(x => x.Id).Distinct().Count() == 2, "Bloque seleccionado, recursos repetidos, formatos y destinos");
Check(!MostazaMaterialReader.Permitida(new Uri("https://www.mostazaweb.com.ar:123/oferta.png")), "No otros puertos");
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Ocr:ApiKey"]="prueba-ocr", ["Ia:ApiKey"]="prueba-ia" }).Build();
var handler = new Fake(); using var http = new HttpClient(handler);
var reader = new MostazaMaterialReader(http, new OcrSpaceReader(http,config),new GeminiOfferReader(http,config),config,NullLogger<MostazaMaterialReader>.Instance,new MostazaMaterialCache());
var first = await reader.LeerAsync(html,CancellationToken.None);
Check(first.Count == 2 && handler.OcrCalls == 2 && handler.IaCalls == 2 && first.All(x => x.Lectura.Publicacion.Beneficios.Single().Valor==30), "Descarga automática, OCR de imagen y PDF, clasificación");
await reader.LeerAsync(html,CancellationToken.None);
Check(handler.OcrCalls == 2 && handler.IaCalls == 2, "No reprocesar contenido idéntico");
handler.Invalid = true;
Check((await reader.LeerAsync(html,CancellationToken.None)).Count == 0, "Un archivo inválido no reemplaza su ficha");
handler.Invalid = false; handler.EvilRedirect = true;
try { await reader.Descargar(found[0].Url,CancellationToken.None);throw new Exception("Debía rechazar redirección externa"); }
catch(InvalidDataException) { }
Check(handler.BadHostCalls == 0, "No contactar destino externo");
Console.WriteLine("OK: selección de recursos, imagen/PDF → OCR → IA, caché y rechazo de archivos/redirecciones verificados.");
static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
sealed class Fake:HttpMessageHandler
{
    public int OcrCalls,IaCalls,BadHostCalls; public bool Invalid,EvilRedirect;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        var host=request.RequestUri!.Host;
        if(host=="www.mostazaweb.com.ar")
        {
            if(EvilRedirect){var r=new HttpResponseMessage(HttpStatusCode.Found);r.Headers.Location=new Uri("https://127.0.0.1/oferta.png");return Task.FromResult(r);}
            var content=new ByteArrayContent(Invalid?new byte[OcrSpaceReader.MaxBytes+1]:new byte[]{1,2,3});
            content.Headers.ContentType=new MediaTypeHeaderValue(request.RequestUri.AbsolutePath.EndsWith(".pdf")?"application/pdf":"image/png");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=content});
        }
        if(host=="api.ocr.space")
        {
            OcrCalls++;
            return Json(new {OCRExitCode=1,IsErroredOnProcessing=false,ParsedResults=new[]{new{FileParseExitCode=1,ParsedText="30% de descuento en combo. Válido en locales adheridos."}}});
        }
        if(host=="generativelanguage.googleapis.com")
        {
            IaCalls++;
            var p=new{esOferta=true,requiereRevision=false,nombre="Combo con descuento",resumen="30% de descuento en combo.",condiciones="Válido en locales adheridos.",evidenciaCondiciones="Válido en locales adheridos.",desde=(string?)null,hasta=(string?)null,requiereApp=(bool?)null,evidenciaApp=(string?)null,requiereSesion=(bool?)null,evidenciaSesion=(string?)null,beneficios=new[]{new{tipo="Porcentaje",descripcion="30% de descuento",valor=30,moneda=(string?)null,evidencia="30% de descuento en combo."}}};
            return Json(new{candidates=new[]{new{finishReason="STOP",content=new{parts=new[]{new{text=JsonSerializer.Serialize(p)}}}}}});
        }
        BadHostCalls++;throw new Exception("Host inesperado");
    }
    private static Task<HttpResponseMessage> Json(object data)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(data),Encoding.UTF8,"application/json")});
}
