using Cazador.Api.Infrastructure.Persistence;
using Cazador.Api.Modules.Catalogo.Entities;
using Cazador.Api.Modules.Obtencion.Readers;
using Microsoft.EntityFrameworkCore;

namespace Cazador.Api.Background;

public sealed class MostazaWorker(IServiceScopeFactory scopes, IHttpClientFactory clients,
    IConfiguration config, ILogger<MostazaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue<bool>("Obtencion:Mostaza:Enabled")) return;
        var horas = config.GetValue<double>("Obtencion:Mostaza:IntervalHours", 6);
        if (!double.IsFinite(horas) || horas < 1 || horas > 168)
        {
            logger.LogError("IntervalHours debe estar entre 1 y 168. Lectura de Mostaza desactivada.");
            return;
        }
        // Una ejecución a la vez, al iniciar y luego con espera entre ciclos.
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Actualizar(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // No registrar mensajes de conexión que podrían contener datos de acceso.
                logger.LogError("Falló la lectura o persistencia de Mostaza ({Tipo}). Se mantiene el catálogo anterior.", ex.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromHours(horas), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task Actualizar(CancellationToken ct)
    {
        using var response = await clients.CreateClient("Mostaza").GetAsync(MostazaReader.Url, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType != "text/html")
            throw new InvalidDataException("Mostaza: respuesta no HTML.");
        var html = await response.Content.ReadAsStringAsync(ct);
        if (html.Length > 2_000_000) throw new InvalidDataException("Mostaza: respuesta demasiado grande.");
        var leidas = MostazaReader.Leer(html);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var fuente = await db.Fuentes.SingleOrDefaultAsync(x => x.Marca == "Mostaza" && x.Url == MostazaReader.Url, ct);
        if (fuente is null)
        {
            fuente = new Fuente { Marca = "Mostaza", Url = MostazaReader.Url };
            db.Fuentes.Add(fuente);
        }
        var actuales = fuente.Id == 0 ? new List<Oferta>() : await db.Ofertas.Include(x => x.Beneficios)
            .Where(x => x.FuenteId == fuente.Id).ToListAsync(ct);
        var ahora = DateTime.UtcNow;
        foreach (var leida in leidas)
        {
            var oferta = actuales.SingleOrDefault(x => x.IdentificadorPublicacion == leida.Identificador);
            if (oferta is null)
            {
                oferta = new Oferta { Fuente = fuente, IdentificadorPublicacion = leida.Identificador };
                db.Ofertas.Add(oferta);
            }
            oferta.Nombre = leida.Nombre;
            // Extracto literal: no resume ni interpreta condiciones con IA en esta etapa.
            oferta.Resumen = string.Join(" ", leida.Beneficios.Select(b => b.Descripcion));
            oferta.Condiciones = leida.Condiciones;
            oferta.TextoOriginal = leida.Nombre + "\n" + leida.Condiciones;
            oferta.VigenteDesde = leida.Desde; oferta.VigenteHasta = leida.Hasta;
            oferta.UrlOriginal = MostazaReader.Url;
            oferta.EsPaginaGeneral = true;
            oferta.UltimaLecturaUtc = ahora;
            oferta.EnlaceDisponible = true; oferta.UltimaComprobacionUtc = ahora;
            // Una página pública no demuestra los requisitos de canje: se mantienen desconocidos.
            db.Beneficios.RemoveRange(oferta.Beneficios);
            oferta.Beneficios.Clear();
            foreach (var beneficio in leida.Beneficios)
                oferta.Beneficios.Add(new Beneficio { Tipo = TipoBeneficio.PromocionBancaria,
                    Descripcion = beneficio.Descripcion, Valor = beneficio.Porcentaje });
        }
        // Ausencia en la respuesta no equivale a vencimiento. Las fechas conocidas se filtran en el catálogo.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Mostaza: {Cantidad} publicaciones leídas y actualizadas a las {FechaUtc} UTC.", leidas.Count, ahora);
    }
}
