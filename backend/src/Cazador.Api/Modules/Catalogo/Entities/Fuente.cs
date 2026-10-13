namespace Cazador.Api.Modules.Catalogo.Entities;

public class Fuente
{
    public long Id { get; set; }
    public string Marca { get; set; } = "";
    public string Url { get; set; } = "";
    public List<Oferta> Ofertas { get; set; } = [];
}
