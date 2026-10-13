using Cazador.Api.Modules.Obtencion.Readers;

namespace Cazador.Api.Modules.Obtencion.Ia;

public static class MostazaIaPolicy
{
    public static void Validar(OfertaLeida origen, PublicacionIa interpretacion)
    {
        if (!interpretacion.EsOferta || interpretacion.RequiereRevision)
            throw new InvalidDataException("Mostaza: clasificación dudosa; conservar ficha anterior.");
        // La IA enriquece la ficha, pero no reemplaza el porcentaje extraído de la fuente.
        var esperados = origen.Beneficios.Select(x => x.Porcentaje).Distinct().Order().ToArray();
        var leidos = interpretacion.Beneficios.Where(x => x.Tipo == "PromocionBancaria" || x.Tipo == "Porcentaje")
            .Select(x => x.Valor).Where(x => x.HasValue).Select(x => x!.Value).Distinct().Order().ToArray();
        if (!esperados.SequenceEqual(leidos) || interpretacion.Beneficios.Any(x => x.Tipo is not "PromocionBancaria" and not "Porcentaje"))
            throw new InvalidDataException("Mostaza: la IA cambió el beneficio bancario; conservar ficha anterior.");
        if (origen.Desde is not null && interpretacion.Desde != origen.Desde.Value.ToString("yyyy-MM-dd")
            || origen.Hasta is not null && interpretacion.Hasta != origen.Hasta.Value.ToString("yyyy-MM-dd"))
            throw new InvalidDataException("Mostaza: la IA cambió u omitió una fecha conocida; conservar ficha anterior.");
    }
}
