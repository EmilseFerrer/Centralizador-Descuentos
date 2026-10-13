namespace Cazador.Api.Modules.Catalogo.Entities;

public enum TipoBeneficio { PrecioFinal, Porcentaje, MontoDescuento, ProductoGratis, Cupon, PromocionBancaria, Otro }
public class Beneficio
{
    public long Id { get; set; }
    public long OfertaId { get; set; }
    public Oferta Oferta { get; set; } = null!;
    public TipoBeneficio Tipo { get; set; }
    public string Descripcion { get; set; } = "";
    public decimal? Valor { get; set; }
    public string? Moneda { get; set; }
}
