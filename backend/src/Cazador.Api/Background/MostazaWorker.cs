using System.Security.Cryptography;
using System.Text;
using Cazador.Api.Modules.Obtencion.Ia;
using Cazador.Api.Infrastructure.Persistence;
using Cazador.Api.Modules.Catalogo.Entities;
using Cazador.Api.Modules.Obtencion.Readers;
using Microsoft.EntityFrameworkCore;

namespace Cazador.Api.Background;

public sealed class MostazaWorker(IServiceScopeFactory scopes, IHttpClientFactory clients,
    IConfiguration config, ILogger<MostazaWorker> logger) : BackgroundService
{
    // Máximo 100 interpretaciones en memoria; evita releer con IA texto idéntico
    // durante esta ejecución del backend. Al reiniciar se vuelve a interpretar.
    private readonly Dictionary<string, PublicacionIa> interpretaciones = new();

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
        var fuente = await db.Fuentes.SingleOrDefaultAsync(x => x.Marca == "Mostaza" && x.Url == MostazaReader.Url, ct);
        if (fuente is null)
        {
            fuente = new Fuente { Marca = "Mostaza", Url = MostazaReader.Url };
            db.Fuentes.Add(fuente);
        }
        var actuales = fuente.Id == 0 ? new List<Oferta>() : await db.Ofertas.Include(x => x.Beneficios)
            .Where(x => x.FuenteId == fuente.Id).ToListAsync(ct);
        var preparadas = new List<(OfertaLeida Oferta, PublicacionIa? Ia)>();
        var usarIa = config.GetValue<bool>("Obtencion:Mostaza:UseIa");
        var iaReader = scope.ServiceProvider.GetRequiredService<GeminiOfferReader>();
        var cuotaAgotada = false;
        foreach (var leida in leidas)
        {
            if (!usarIa) { preparadas.Add((leida, null)); continue; }
            if (cuotaAgotada) continue;
            var original = leida.Nombre + "\n" + leida.Condiciones;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                (config["Ia:Modelo"] ?? GeminiOfferReader.ModeloPredeterminado) + "\n" + original)));
            try
            {
                if (!interpretaciones.TryGetValue(hash, out var interpretacion))
                {
                    interpretacion = (await iaReader.LeerAsync(original, ct)).Publicacion;
                    MostazaIaPolicy.Validar(leida, interpretacion);
                    if (interpretaciones.Count >= 100) interpretaciones.Clear();
                    interpretaciones[hash] = interpretacion;
                }
                preparadas.Add((leida, interpretacion));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // No registrar claves, respuestas del proveedor ni información de conexión.
                logger.LogWarning("Mostaza: no se publicará la interpretación de {Id} ({Tipo}); se conserva la ficha anterior.",
                    leida.Identificador, ex.GetType().Name);
                if (ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests }
                    || ex is InvalidOperationException) cuotaAgotada = true;
            }
        }
        // Ninguna transacción de escritura queda abierta durante llamadas a IA.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var ahora = DateTime.UtcNow;
        foreach (var (leida, interpretacion) in preparadas)
        {
            var oferta = actuales.SingleOrDefault(x => x.IdentificadorPublicacion == leida.Identificador);
            if (oferta is null)
            {
                oferta = new Oferta { Fuente = fuente, IdentificadorPublicacion = leida.Identificador };
                db.Ofertas.Add(oferta);
            }
            oferta.Nombre = leida.Nombre;
            // Resumen interpretado; todas las condiciones originales siguen visibles.
            oferta.Resumen = interpretacion?.Resumen ?? string.Join(" ", leida.Beneficios.Select(b => b.Descripcion));
            if (interpretacion is not null)
            {
                oferta.RequiereApp = interpretacion.RequiereApp;
                oferta.RequiereSesion = interpretacion.RequiereSesion;
            }
            oferta.Condiciones = leida.Condiciones;
            oferta.TextoOriginal = leida.Nombre + "\n" + leida.Condiciones;
            oferta.VigenteDesde = leida.Desde; oferta.VigenteHasta = leida.Hasta;
            oferta.UrlOriginal = MostazaReader.Url;
            oferta.EsPaginaGeneral = true;
            oferta.UltimaLecturaUtc = ahora;
            oferta.EnlaceDisponible = true; oferta.UltimaComprobacionUtc = ahora;
            // Solo se asignan requisitos con evidencia específica en la publicación.
            db.Beneficios.RemoveRange(oferta.Beneficios);
            oferta.Beneficios.Clear();
            foreach (var beneficio in leida.Beneficios)
                oferta.Beneficios.Add(new Beneficio { Tipo = TipoBeneficio.PromocionBancaria,
                    Descripcion = beneficio.Descripcion, Valor = beneficio.Porcentaje });
        }
        // Ausencia en la respuesta no equivale a vencimiento. Las fechas conocidas se filtran en el catálogo.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Mostaza: {Cantidad} publicaciones actualizadas (IA: {UsarIa}) a las {FechaUtc} UTC.", preparadas.Count, usarIa, ahora);
    }
}
