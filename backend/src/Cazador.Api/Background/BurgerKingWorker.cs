using System.Globalization;
using System.Text.Json;
using Cazador.Api.Infrastructure.Persistence;
using Cazador.Api.Modules.Catalogo.Entities;
using Cazador.Api.Modules.Obtencion.Readers;
using Microsoft.EntityFrameworkCore;
namespace Cazador.Api.Background;

public sealed class BurgerKingWorker(IServiceScopeFactory scopes, IHttpClientFactory clients, IConfiguration config,
    ILogger<BurgerKingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("Obtencion:BurgerKing:Enabled")) return;
        var hours = config.GetValue<double>("Obtencion:BurgerKing:IntervalHours", 6);
        if (!double.IsFinite(hours) || hours < 1 || hours > 168) { logger.LogError("Burger King: intervalo inválido."); return; }
        while (!ct.IsCancellationRequested)
        {
            try { await Actualizar(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Burger King: falló lectura o persistencia ({Tipo}). Se conserva el catálogo anterior.", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromHours(hours), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
    private async Task<string> Download(HttpClient client, string url, CancellationToken ct)
    {
        using var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(ct);
        if (text.Length > 4_000_000) throw new InvalidDataException("Burger King: respuesta demasiado grande.");
        return text;
    }
    private async Task Actualizar(CancellationToken ct)
    {
        var client = clients.CreateClient("BurgerKing");
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        client.DefaultRequestHeaders.Remove("x-user-datetime");
        client.DefaultRequestHeaders.Add("x-user-datetime", TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture));
        var engine = await Download(client, BurgerKingReader.EngineUrl + "?query=" + Uri.EscapeDataString(BurgerKingReader.EngineQuery), ct);
        var active = BurgerKingReader.Activas(engine);
        var ids = active.EnumerateArray().Select(x => BurgerKingReader.String(x, "sanityId")).Where(x => x.Length > 0).Distinct().ToArray();
        if (ids.Length == 0) { logger.LogWarning("Burger King: no hay identificadores públicos; se mantiene el catálogo anterior."); return; }
        var cmsUrl = BurgerKingReader.CmsUrl + "?query=" + Uri.EscapeDataString(BurgerKingReader.CmsQuery) + "&%24ids=" + Uri.EscapeDataString(JsonSerializer.Serialize(ids));
        var cms = await Download(client, cmsUrl, ct);
        var offers = BurgerKingReader.Leer(engine, cms, DateTimeOffset.UtcNow);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var source = await db.Fuentes.SingleOrDefaultAsync(x => x.Marca == "Burger King" && x.Url == BurgerKingReader.Url, ct);
        if (source is null) { source = new Fuente { Marca = "Burger King", Url = BurgerKingReader.Url }; db.Fuentes.Add(source); }
        var current = source.Id == 0 ? new List<Oferta>() : await db.Ofertas.Include(x => x.Beneficios).Where(x => x.FuenteId == source.Id).ToListAsync(ct);
        foreach (var item in offers)
        {
            var offer = current.SingleOrDefault(x => x.IdentificadorPublicacion == item.Id);
            if (offer is null) { offer = new Oferta { Fuente = source, IdentificadorPublicacion = item.Id }; db.Ofertas.Add(offer); }
            offer.Nombre = item.Nombre; offer.Resumen = item.Resumen; offer.Condiciones = item.Condiciones; offer.TextoOriginal = item.Original;
            offer.VigenteDesde = item.Desde; offer.VigenteHasta = item.Hasta; offer.UltimaLecturaUtc = DateTime.UtcNow;
            offer.UrlOriginal = BurgerKingReader.Url; offer.EsPaginaGeneral = true;
            // El acceso a la API no comprueba que el navegador pueda abrir la página original.
            // Abrir conserva su comprobación HTTP independiente y no infiere requisitos de cuenta/app.
            db.Beneficios.RemoveRange(offer.Beneficios); offer.Beneficios.Clear();
            foreach (var b in item.Beneficios) offer.Beneficios.Add(new Beneficio { Tipo = b.Tipo, Descripcion = b.Descripcion, Valor = b.Valor, Moneda = b.Moneda });
        }
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        logger.LogInformation("Burger King: {Cantidad} publicaciones actualizadas; origen público, sin autenticación.", offers.Count);
    }
}
