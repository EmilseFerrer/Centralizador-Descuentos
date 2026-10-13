using Cazador.Api.Modules.Catalogo.Entities;
namespace Cazador.Api.Modules.Participacion.Entities;

public class Apertura
{
    public Guid Id { get; set; }
    public long OfertaId { get; set; }
    public Oferta Oferta { get; set; } = null!;
    public string UrlDestino { get; set; } = "";
    public DateTime FechaUtc { get; set; }
    public bool? Encontrada { get; set; }
    public DateTime? ConfirmacionUtc { get; set; }
}

public class ReporteOferta
{
    public long Id { get; set; }
    public long OfertaId { get; set; }
    public Oferta Oferta { get; set; } = null!;
    public string Motivo { get; set; } = "";
    public DateTime FechaUtc { get; set; }
}
