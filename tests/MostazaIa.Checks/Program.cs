using System.Net;
using System.Reflection;
using System.Text.Json;
using Cazador.Api.Background;
using Cazador.Api.Infrastructure.Persistence;
using Cazador.Api.Modules.Obtencion.Ia;
using Cazador.Api.Modules.Obtencion.Ocr;
using Cazador.Api.Modules.Obtencion.Materiales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Ia:ApiKey"]="prueba",["Obtencion:Mostaza:UseIa"]="true",["Obtencion:Mostaza:UseMateriales"]="true",["Ocr:ApiKey"]="prueba-ocr"}).Build();
var handler = new Handler();
var services = new ServiceCollection().AddDbContext<AppDbContext>(o=>o.UseSqlite(connection))
    .AddSingleton(new GeminiOfferReader(new HttpClient(handler),config))
    .AddSingleton(new MostazaMaterialReader(new HttpClient(handler),new OcrSpaceReader(new HttpClient(handler),config),
        new GeminiOfferReader(new HttpClient(handler),config),config,NullLogger<MostazaMaterialReader>.Instance,new MostazaMaterialCache()));
using var provider = services.BuildServiceProvider();
using (var scope=provider.CreateScope()) scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
var worker = new MostazaWorker(provider.GetRequiredService<IServiceScopeFactory>(),new Factory(handler),config,NullLogger<MostazaWorker>.Instance);
var method=typeof(MostazaWorker).GetMethod("Actualizar",BindingFlags.Instance|BindingFlags.NonPublic)!;
async Task Cycle()=>await (Task)method.Invoke(worker,new object[]{CancellationToken.None})!;
await Cycle();
using(var scope=provider.CreateScope()){
 var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var offer=db.Ofertas.Single(x=>!x.IdentificadorPublicacion.StartsWith("material:"));
 if(offer.Resumen!="Resumen IA del 30%"||offer.Condiciones!="30% de ahorro. Válido únicamente para Take Away.")throw new Exception("No preservó resumen y condiciones completas");
}
await Cycle();
using(var scope=provider.CreateScope()) if(scope.ServiceProvider.GetRequiredService<AppDbContext>().Ofertas.Count()!=2||handler.Calls!=2)throw new Exception("Duplicó oferta o consumió IA repetida");
handler.Changed=true;handler.Fail=true;await Cycle();
using(var scope=provider.CreateScope()){
 var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var offer=db.Ofertas.Single(x=>!x.IdentificadorPublicacion.StartsWith("material:"));
 if(offer.Resumen!="Resumen IA del 30%"||offer.Condiciones!.Contains("Cambio"))throw new Exception("Fallo IA sobrescribió la ficha anterior");
}
Console.WriteLine("OK integración: texto e imagen → OCR → IA → persistencia, identidad estable, caché y preservación ante HTTP 429.");
sealed class Factory(Handler h):IHttpClientFactory {public HttpClient CreateClient(string n)=>new(h,false);}
sealed class Handler:HttpMessageHandler{
 public int Calls;public bool Changed;public bool Fail;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct){
  if(r.Method==HttpMethod.Get && r.RequestUri!.AbsolutePath=="/oferta.png")
  {
   var content=new ByteArrayContent(new byte[]{1,2,3}); content.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
   return new HttpResponseMessage(HttpStatusCode.OK){Content=content};
  }
  if(r.RequestUri!.Host=="api.ocr.space")
   return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{OCRExitCode=1,IsErroredOnProcessing=false,ParsedResults=new[]{new{FileParseExitCode=1,ParsedText="30% de ahorro. Válido únicamente para Take Away."}}}),System.Text.Encoding.UTF8,"application/json")};
  if(r.Method==HttpMethod.Get)return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("<div data-widget_type='text-editor.default'><p><strong>Banco Prueba</strong></p><p>30% de ahorro. Válido únicamente para Take Away."+(Changed?" Cambio publicado.":"")+"</p><img alt='Promoción de comida' src='/oferta.png'></div>",System.Text.Encoding.UTF8,"text/html")};
  Calls++;
  using var body = JsonDocument.Parse(await r.Content!.ReadAsStringAsync(ct));
  if (body.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()!.Contains("CONTEXTO DE PUBLICACIÓN BANCARIA"))
  {
  if (body.RootElement.GetProperty("generationConfig").GetProperty("responseJsonSchema").GetProperty("properties").GetProperty("condiciones").GetProperty("type").GetString() != "null")
    throw new Exception("El contexto bancario debe conservar las condiciones originales");
  var beneficioSchema = body.RootElement.GetProperty("generationConfig").GetProperty("responseJsonSchema").GetProperty("properties").GetProperty("beneficios").GetProperty("items").GetProperty("properties");
  if (beneficioSchema.GetProperty("moneda").GetProperty("type").GetString() != "null" || beneficioSchema.GetProperty("valor").GetProperty("maximum").GetInt32() != 100)
    throw new Exception("Un porcentaje bancario no lleva moneda y no supera 100");
  }
  if(Fail)return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
  var output=new{esOferta=true,requiereRevision=false,nombre="Banco Prueba",resumen="Resumen IA del 30%",condiciones=(string?)null,evidenciaCondiciones=(string?)null,desde=(string?)null,hasta=(string?)null,requiereApp=(bool?)null,evidenciaApp=(string?)null,requiereSesion=(bool?)null,evidenciaSesion=(string?)null,beneficios=new[]{new{tipo="PromocionBancaria",descripcion="30% de ahorro",valor=30,moneda=(string?)null,evidencia="30% de ahorro."}}};
  var payload=new{candidates=new[]{new{finishReason="STOP",content=new{parts=new[]{new{text=JsonSerializer.Serialize(output)}}}}}};
  return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(payload),System.Text.Encoding.UTF8,"application/json")};
 }
}
