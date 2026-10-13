using Cazador.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cazador.Api.Modules.Catalogo.Controllers;

[ApiController]
[Route("api/ofertas")]
public class OfertasController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Argentina/Buenos_Aires"));
        var ofertas = await db.Ofertas.AsNoTracking()
            .Where(x => x.VigenteHasta == null || x.VigenteHasta >= hoy)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Marca = x.Fuente.Marca, x.Nombre, x.Resumen, x.VigenteHasta })
            .ToListAsync(cancellationToken);
        return Ok(ofertas);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalle(long id, CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Argentina/Buenos_Aires"));
        var oferta = await db.Ofertas.AsNoTracking()
            .Where(x => x.Id == id && (x.VigenteHasta == null || x.VigenteHasta >= hoy))
            .Select(x => new {
                x.Id, Marca = x.Fuente.Marca, x.Nombre, x.Resumen, x.Condiciones,
                x.VigenteDesde, x.VigenteHasta, x.UltimaLecturaUtc,
                Fuente = x.Fuente.Url, x.UrlOriginal, x.RequiereApp, x.RequiereSesion,
                x.EsPaginaGeneral, x.EnlaceDisponible, x.UltimaComprobacionUtc,
                Beneficios = x.Beneficios.Select(b => new { Tipo = b.Tipo.ToString(), b.Descripcion, b.Valor, b.Moneda })
            }).FirstOrDefaultAsync(cancellationToken);
        return oferta is null ? NotFound() : Ok(oferta);
    }
}
