using Cazador.Api.Modules.Catalogo.Entities;
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
            .Select(x => new { x.Id, Marca = x.Fuente.Marca, x.Nombre, x.Resumen, x.VigenteHasta, x.TextoOriginal, x.Condiciones, EsBancaria = x.Beneficios.Any(b => b.Tipo == TipoBeneficio.PromocionBancaria) })
            .ToListAsync(cancellationToken);
        return Ok(ofertas.Select(x => new { x.Id, x.Marca, x.Nombre, x.Resumen, x.VigenteHasta, x.Condiciones, x.EsBancaria, Imagen = OfertaImagen.Leer(x.Marca, x.TextoOriginal) }));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalle(long id, CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Argentina/Buenos_Aires"));
        var oferta = await db.Ofertas.AsNoTracking()
            .Where(x => x.Id == id && (x.VigenteHasta == null || x.VigenteHasta >= hoy))
            .Select(x => new {
                x.Id, Marca = x.Fuente.Marca, x.Nombre, x.Resumen, x.Condiciones, x.TextoOriginal,
                x.VigenteDesde, x.VigenteHasta, x.UltimaLecturaUtc,
                Fuente = x.Fuente.Url, x.UrlOriginal, x.RequiereApp, x.RequiereSesion,
                x.EsPaginaGeneral, x.EnlaceDisponible, x.UltimaComprobacionUtc,
                EsBancaria = x.Beneficios.Any(b => b.Tipo == TipoBeneficio.PromocionBancaria),
                Beneficios = x.Beneficios.Select(b => new { Tipo = b.Tipo.ToString(), b.Descripcion, b.Valor, b.Moneda })
            }).FirstOrDefaultAsync(cancellationToken);
        if (oferta is null) return NotFound();
        return Ok(new { oferta.Id, oferta.Marca, oferta.Nombre, oferta.Resumen, oferta.Condiciones,
            oferta.VigenteDesde, oferta.VigenteHasta, oferta.UltimaLecturaUtc,
            oferta.Fuente, oferta.UrlOriginal, oferta.RequiereApp, oferta.RequiereSesion,
            oferta.EsPaginaGeneral, oferta.EnlaceDisponible, oferta.UltimaComprobacionUtc, oferta.Beneficios, oferta.EsBancaria,
            Imagen = OfertaImagen.Leer(oferta.Marca, oferta.TextoOriginal) });
    }
}
