using System.ComponentModel.DataAnnotations;
using System.Net;
using Cazador.Api.Infrastructure.Persistence;
using Cazador.Api.Modules.Catalogo.Entities;
using Cazador.Api.Modules.Participacion.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

void Check(bool value, string message) { if (!value) throw new Exception(message); }
bool Valid(object value) => Validator.TryValidateObject(value, new ValidationContext(value), new List<ValidationResult>(), true);
using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
await db.Database.EnsureCreatedAsync();
var source = new Fuente { Marca = "Mostaza", Url = "https://www.mostazaweb.com.ar/" };
var active = new Oferta { Fuente = source, IdentificadorPublicacion = "activa", Nombre = "Oferta", UrlOriginal = source.Url };
var expired = new Oferta { Fuente = source, IdentificadorPublicacion = "vencida", Nombre = "Vencida", UrlOriginal = source.Url, VigenteHasta = new DateOnly(2000,1,1) };
db.Ofertas.AddRange(active, expired); await db.SaveChangesAsync();
var factory = new Factory();
var controller = new ParticipacionController(db, factory);
Check(!Valid(new ReporteRequest { Motivo = "otro" }) && Valid(new ReporteRequest { Motivo = "vencida" }), "Validación de motivo.");
Check(!Valid(new ConfirmacionRequest()) && Valid(new ConfirmacionRequest { Encontrada = false }), "No confundir ausencia con negativa.");
Check(!ParticipacionController.DestinoPermitido("https://mostazaweb.com.ar.evil.example/") && !ParticipacionController.DestinoPermitido("http://mostazaweb.com.ar/") && ParticipacionController.DestinoPermitido(source.Url), "Validación del destino oficial.");
Check(await controller.Abrir(expired.Id, default) is NotFoundObjectResult, "Rechazar apertura de vencida.");
Check(await controller.Abrir(active.Id, default) is OkObjectResult, "Apertura válida.");
var apertura = await db.Aperturas.SingleAsync();
Check(apertura.OfertaId == active.Id && apertura.Encontrada is null, "Apertura sin respuesta registrada.");
await controller.Confirmar(apertura.Id, new ConfirmacionRequest { Encontrada = false }, default);
var date = apertura.ConfirmacionUtc;
await controller.Confirmar(apertura.Id, new ConfirmacionRequest { Encontrada = false }, default);
Check(apertura.Encontrada == false && apertura.ConfirmacionUtc == date, "Confirmación idempotente.");
Check(await controller.Confirmar(Guid.NewGuid(), new ConfirmacionRequest { Encontrada = true }, default) is NotFoundObjectResult, "Apertura inexistente.");
Check(await controller.Reportar(expired.Id, new ReporteRequest { Motivo = "vencida" }, default) is ObjectResult { StatusCode: 201 }, "Permitir reporte de vencida consultada anteriormente.");
Check(await db.Reportes.CountAsync() == 1 && await db.Ofertas.CountAsync() == 2, "Reporte no retira oferta.");
factory.Status = HttpStatusCode.NotFound;
Check(await controller.Abrir(active.Id, default) is ConflictObjectResult && await db.Aperturas.CountAsync() == 1 && active.EnlaceDisponible == false, "Destino caído no crea apertura.");
factory.Status = HttpStatusCode.Forbidden;
await controller.Abrir(active.Id, default);
Check(active.EnlaceDisponible is null && active.RequiereSesion is null, "403 no implica inicio de sesión.");
factory.Throw = true;
Check(await controller.Abrir(active.Id, default) is ObjectResult { StatusCode: 503 }, "Fallo de red comunicado.");
Console.WriteLine("Participación: validaciones, persistencia, ausencia de respuesta, idempotencia y fallos aprobados en SQLite temporal. MySQL se verifica al aplicar la migración.");

class Factory : IHttpClientFactory
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public bool Throw { get; set; }
    public HttpClient CreateClient(string name) => new(new Handler(this));
}
class Handler(Factory factory) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (factory.Throw) throw new HttpRequestException();
        return Task.FromResult(new HttpResponseMessage(factory.Status) { RequestMessage = request });
    }
}
