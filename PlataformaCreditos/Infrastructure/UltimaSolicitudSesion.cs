using System.Text.Json;

namespace PlataformaCreditos.Infrastructure;

/// <summary>Última solicitud visitada, guardada en la sesión (respaldada por Redis).</summary>
public record UltimaSolicitudVisitada(string UsuarioId, int SolicitudId, decimal Monto);

public static class UltimaSolicitudSesion
{
    private const string Clave = "UltimaSolicitudVisitada";

    public static void Guardar(ISession session, UltimaSolicitudVisitada valor) =>
        session.SetString(Clave, JsonSerializer.Serialize(valor));

    /// <summary>Devuelve el valor solo si pertenece al usuario actual (la sesión es por navegador).</summary>
    public static UltimaSolicitudVisitada? Obtener(ISession session, string? usuarioId)
    {
        var json = session.GetString(Clave);
        if (json is null || usuarioId is null)
        {
            return null;
        }

        var valor = JsonSerializer.Deserialize<UltimaSolicitudVisitada>(json);
        return valor?.UsuarioId == usuarioId ? valor : null;
    }
}
