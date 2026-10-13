using Cazador.Api.Modules.Obtencion.Readers;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
string Wrap(string body) => "<div data-widget_type='text-editor.default'>" + body + "</div>";
var html = Wrap("<p><b>PROMOCIÓN «BANCO EJEMPLO»</b></p><p>Válida del 03/08/2026 al 31/12/2026.</p><p>25% de reintegro, con tope $8.000.</p><p><b>BANCO SEGUNDO</b></p><ul><li>20% de ahorro con débito.</li><li>30% de reintegro con crédito.</li></ul><p><b>BASES Y CONDICIONES CONCURSO</b></p><p>70% de descuento como premio aleatorio.</p>");
var leidas = MostazaReader.Leer(html);
Check(leidas.Count == 2, "Debe excluir el concurso.");
Check(leidas[0].Desde == new DateOnly(2026,8,3) && leidas[0].Hasta == new DateOnly(2026,12,31), "Normalización de fechas.");
Check(leidas[1].Hasta is null && leidas[1].Beneficios.Count == 2, "Fecha desconocida y beneficios alternativos.");
Check(leidas[0].Beneficios[0].Porcentaje == 25, "No confundir porcentaje con tope.");
Check(MostazaReader.Leer(html.Replace("BANCO EJEMPLO", "Banco Ejemplo"))[0].Identificador == leidas[0].Identificador, "Identidad estable ante cambios de mayúsculas.");
foreach (var invalido in new[] { "<html>vacío</html>", Wrap("<p><b>BANCO EJEMPLO</b></p><p>Sin beneficio.</p>"), html.Replace("31/12/2026", "31/02/2026"), html.Replace("25%", "125%"), html.Replace("BANCO SEGUNDO", "BANCO EJEMPLO") })
{
    var rejected = false;
    try { MostazaReader.Leer(invalido); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Debe rechazar estructura, beneficio, fecha o identidad inválidos.");
}
if (args.Length > 0)
{
    var reales = MostazaReader.Leer(File.ReadAllText(args[0]));
    Check(reales.Count == 5, "La página inspeccionada debe producir cinco publicaciones bancarias.");
    Check(reales.Single(x => x.Nombre.Contains("PROVINCIA")).Beneficios.Count == 2, "Provincia tiene dos beneficios.");
    Console.WriteLine($"Fuente real: {reales.Count} publicaciones, {reales.Sum(x => x.Beneficios.Count)} beneficios.");
}
Console.WriteLine("Comprobaciones del lector aprobadas.");
