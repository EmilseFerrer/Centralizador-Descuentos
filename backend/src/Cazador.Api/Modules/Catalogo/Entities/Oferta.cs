namespace Cazador.Api.Modules.Catalogo.Entities;

public class Oferta
{
    public long Id { get; set; }
    public long FuenteId { get; set; }
    public Fuente Fuente { get; set; } = null!;
    public string IdentificadorPublicacion { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Resumen { get; set; } = "";
    public string TextoOriginal { get; set; } = "";
    public string? Condiciones { get; set; }
    public DateOnly? VigenteDesde { get; set; }
    public DateOnly? VigenteHasta { get; set; }
    public DateTime UltimaLecturaUtc { get; set; }
    public string UrlOriginal { get; set; } = "";
    public bool? RequiereApp { get; set; }
    public bool? RequiereSesion { get; set; }
    public bool? EsPaginaGeneral { get; set; }
    public bool? EnlaceDisponible { get; set; }
    public DateTime? UltimaComprobacionUtc { get; set; }
    public List<Beneficio> Beneficios { get; set; } = [];
}
