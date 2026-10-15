using System.Text.Json;

namespace Cazador.Api.Modules.Catalogo;

public sealed record ImagenOferta(string Url, string? Descripcion);

public static class OfertaImagen
{
    public static ImagenOferta? Leer(string marca, string original)
    {
        if (marca == "Burger King")
        {
            try
            {
                using var doc = JsonDocument.Parse(original);
                if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("publicacion", out var p)
                    || p.ValueKind != JsonValueKind.Object
                    || !p.TryGetProperty("imagenUrl", out var image) || image.ValueKind != JsonValueKind.String) return null;
                var url = image.GetString();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https"
                    || uri.Host != "cdn.sanity.io" || !uri.IsDefaultPort || uri.UserInfo.Length != 0
                    || !uri.AbsolutePath.StartsWith("/images/czqk28jt/prod_bk_ar/", StringComparison.Ordinal)) return null;
                var description = p.TryGetProperty("imagenDescripcion", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
                return new ImagenOferta(uri.AbsoluteUri, description);
            }
            catch (JsonException) { return null; }
        }
        if (marca == "Mostaza")
        {
            var firstLine = original.Split('\n', 2)[0];
            if (!Uri.TryCreate(firstLine, UriKind.Absolute, out var uri)
                || !Cazador.Api.Modules.Obtencion.Materiales.MostazaMaterialReader.Permitida(uri)) return null;
            if (Path.GetExtension(uri.AbsolutePath).ToLowerInvariant() is not ".png" and not ".jpg" and not ".jpeg") return null;
            return new ImagenOferta(uri.AbsoluteUri, "Imagen publicada por Mostaza");
        }
        return null;
    }
}
