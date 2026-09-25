using System.Text.Json;

namespace PlataformaCreditos.Messaging;

/// <summary>Mensaje JSON publicado tras guardar una nueva solicitud Pendiente.</summary>
public record SolicitudRegistrada(Guid MessageId, int SolicitudId, string UsuarioId, DateTime FechaEventoUtc)
{
    public const string Tipo = "SolicitudRegistrada";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public byte[] Serializar() => JsonSerializer.SerializeToUtf8Bytes(this);

    /// <summary>Deserializa y valida el mensaje; devuelve null y el motivo si es inválido.</summary>
    public static SolicitudRegistrada? Deserializar(ReadOnlySpan<byte> cuerpo, out string? error)
    {
        SolicitudRegistrada? mensaje;
        try
        {
            mensaje = JsonSerializer.Deserialize<SolicitudRegistrada>(cuerpo, Json);
        }
        catch (JsonException ex)
        {
            error = "JSON inválido: " + ex.Message;
            return null;
        }

        error = mensaje switch
        {
            null => "Mensaje vacío.",
            { MessageId: var id } when id == Guid.Empty => "MessageId ausente o vacío.",
            { SolicitudId: <= 0 } => "SolicitudId inválido.",
            { UsuarioId: var u } when string.IsNullOrWhiteSpace(u) => "UsuarioId ausente.",
            { FechaEventoUtc: var f } when f == default => "FechaEventoUtc ausente.",
            _ => null
        };
        return error is null ? mensaje : null;
    }
}
