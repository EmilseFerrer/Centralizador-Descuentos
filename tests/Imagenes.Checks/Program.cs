using System.Text.Json;
using Cazador.Api.Modules.Catalogo;
string Publicacion(string url)=>JsonSerializer.Serialize(new{publicacion=new{imagenUrl=url,imagenDescripcion="Combo publicado"}});
foreach(var vacia in new[]{"null", "[]", "{\"publicacion\":null}"})
 if(OfertaImagen.Leer("Burger King",vacia)is not null)throw new Exception("Sin imagen no debe fallar el catálogo");
var buena="https://cdn.sanity.io/images/czqk28jt/prod_bk_ar/3c57104bf24c71c00784b79b582954264f5c37d2-1200x799.png";
if(OfertaImagen.Leer("Burger King",Publicacion(buena))?.Url!=buena)throw new Exception("Debe conservar imagen de su publicación");
foreach(var mala in new[]{"javascript:alert(1)","https://otro.example/imagen.png","https://cdn.sanity.io/images/otro/prod/imagen.png","https://cdn.sanity.io:8443/images/czqk28jt/prod_bk_ar/foto.png"})
 if(OfertaImagen.Leer("Burger King",Publicacion(mala))is not null)throw new Exception("No debe exponer imagen ajena al catálogo seleccionado");
if(OfertaImagen.Leer("Burger King","texto antiguo")is not null||OfertaImagen.Leer("Mostaza","Banco Macro\nCondiciones")is not null)throw new Exception("No inventar fotos para publicaciones sin imágenes");
if(OfertaImagen.Leer("Mostaza","https://www.mostazaweb.com.ar/promo.png\nCondiciones")is null)throw new Exception("Conservar imagen original de materiales");
if(OfertaImagen.Leer("Mostaza","https://www.mostazaweb.com.ar/promo.pdf\nCondiciones")is not null)throw new Exception("PDF no es una imagen");
Console.WriteLine("OK: imágenes vinculadas a publicación, fuentes permitidas y ofertas sin fotografía verificados.");
