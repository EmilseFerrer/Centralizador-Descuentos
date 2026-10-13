using System.ComponentModel.DataAnnotations;
using Cazador.Api.Infrastructure.Persistence;
using Cazador.Api.Modules.Participacion.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cazador.Api.Modules.Participacion.Controllers;

public class ReporteRequest
{
    [Required, RegularExpression("^(vencida|datos_incorrectos)$")]
    public string Motivo { get; set; } = "";
}
public class ConfirmacionRequest
{
    [Required]
    public bool? Encontrada { get; set; }
}

[ApiController]
[Route("api")]
public class ParticipacionController(AppDbContext db, IHttpClientFactory clients) : ControllerBase
{
    public static bool DestinoPermitido(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && uri.IsDefaultPort
        && new[] { "mostazaweb.com.ar", "burgerking.com.ar", "mcdonalds.com.ar" }
            .Any(host => uri.Host == host || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));

    [HttpPost("ofertas/{id:long}/aperturas")]
    public async Task<IActionResult> Abrir(long id, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Argentina/Buenos_Aires"));
        var oferta = await db.Ofertas.SingleOrDefaultAsync(x => x.Id == id && (x.VigenteHasta == null || x.VigenteHasta >= hoy), ct);
        if (oferta is null) return NotFound(new { mensaje = "La oferta ya no está disponible." });
        if (!DestinoPermitido(oferta.UrlOriginal)) return Conflict(new { mensaje = "La oferta no tiene un destino oficial admitido." });
        using var response = await Comprobar(oferta.UrlOriginal, ct);
        if (response is null) return StatusCode(503, new { mensaje = "No se pudo comprobar el enlace. Intentá nuevamente." });
        var disponible = response.IsSuccessStatusCode && DestinoPermitido(response.RequestMessage?.RequestUri?.AbsoluteUri ?? "");
        oferta.UltimaComprobacionUtc = DateTime.UtcNow;
        // 401/403 pueden significar sesión o bloqueo de lectura: no inferir requisito de sesión.
        oferta.EnlaceDisponible = response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden ? null : disponible;
        if (!disponible)
        {
            await db.SaveChangesAsync(ct);
            return Conflict(new { mensaje = "No se pudo validar el destino de esta oferta. Su disponibilidad no está confirmada." });
        }
        var apertura = new Apertura { Id = Guid.NewGuid(), OfertaId = id, UrlDestino = oferta.UrlOriginal, FechaUtc = DateTime.UtcNow };
        db.Aperturas.Add(apertura);
        await db.SaveChangesAsync(ct);
        return Ok(new { apertura.Id, apertura.UrlDestino, apertura.FechaUtc, oferta.UltimaComprobacionUtc });
    }

    private async Task<HttpResponseMessage?> Comprobar(string url, CancellationToken ct)
    {
        try
        {
            var client = clients.CreateClient("Enlaces");
            for (var salto = 0; salto < 5; salto++)
            {
                if (!DestinoPermitido(url)) return null;
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    var next = new Uri(new Uri(url), location).AbsoluteUri;
                    response.Dispose(); url = next;
                    continue;
                }
                return response;
            }
            return null;
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
    }

    [HttpPost("ofertas/{id:long}/reportes")]
    public async Task<IActionResult> Reportar(long id, ReporteRequest request, CancellationToken ct)
    {
        // Se acepta reportar una oferta que venció mientras el usuario consultaba su ficha.
        if (!await db.Ofertas.AnyAsync(x => x.Id == id, ct)) return NotFound(new { mensaje = "La oferta no existe." });
        var reporte = new ReporteOferta { OfertaId = id, Motivo = request.Motivo, FechaUtc = DateTime.UtcNow };
        db.Reportes.Add(reporte);
        await db.SaveChangesAsync(ct);
        return StatusCode(201, new { reporte.Id, reporte.FechaUtc });
    }

    [HttpPut("aperturas/{id:guid}/confirmacion")]
    public async Task<IActionResult> Confirmar(Guid id, ConfirmacionRequest request, CancellationToken ct)
    {
        var apertura = await db.Aperturas.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (apertura is null) return NotFound(new { mensaje = "No se encontró la apertura." });
        if (apertura.Encontrada != request.Encontrada)
        {
            apertura.Encontrada = request.Encontrada;
            apertura.ConfirmacionUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Ok(new { apertura.Id, apertura.Encontrada, apertura.ConfirmacionUtc });
    }
}
