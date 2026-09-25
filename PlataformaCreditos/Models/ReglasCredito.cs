namespace PlataformaCreditos.Models;

/// <summary>
/// Reglas de negocio centralizadas para evitar "números mágicos" en controladores y servicios.
/// </summary>
public static class ReglasCredito
{
    /// <summary>Un analista no puede aprobar si el monto supera N veces los ingresos mensuales.</summary>
    public const decimal MultiploMaximoAprobacion = 5m;

    /// <summary>Un cliente no puede registrar una solicitud que supere N veces sus ingresos mensuales.</summary>
    public const decimal MultiploMaximoRegistro = 10m;

    public const int LongitudMaximaMotivo = 500;
}
