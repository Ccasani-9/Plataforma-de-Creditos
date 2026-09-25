using System.Globalization;

namespace PlataformaCreditos.Infrastructure;

/// <summary>Formato de presentación: montos en soles y fechas en hora de Lima (UTC-5).</summary>
public static class Formato
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("en-US");
    private static readonly TimeZoneInfo ZonaLima = ObtenerZonaLima();

    public static string Soles(decimal monto) => "S/ " + monto.ToString("N2", Cultura);

    public static DateTime ALocal(DateTime fechaUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc), ZonaLima);

    public static string Fecha(DateTime fechaUtc) => ALocal(fechaUtc).ToString("dd/MM/yyyy HH:mm", Cultura);

    private static TimeZoneInfo ObtenerZonaLima()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Perú no usa horario de verano: UTC-5 fijo como respaldo si falta tzdata.
            return TimeZoneInfo.CreateCustomTimeZone("America/Lima", TimeSpan.FromHours(-5), "Lima", "Lima");
        }
    }
}
