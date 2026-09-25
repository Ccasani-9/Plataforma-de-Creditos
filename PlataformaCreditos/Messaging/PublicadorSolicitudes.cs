using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace PlataformaCreditos.Messaging;

public record ResultadoPublicacion(bool Exito, Guid MessageId, string? Error = null);

/// <summary>
/// Productor: publica SolicitudRegistrada como mensaje JSON persistente y espera la confirmación
/// del broker (publisher confirms). Con <c>mandatory: true</c> también detecta mensajes no enrutables.
/// </summary>
public class PublicadorSolicitudes(RabbitMqConexion conexion, ILogger<PublicadorSolicitudes> logger)
{
    public async Task<ResultadoPublicacion> PublicarAsync(SolicitudRegistrada mensaje, CancellationToken ct = default)
    {
        var opciones = conexion.Opciones;
        if (!opciones.Configurado)
        {
            logger.LogError("No se publicó {Tipo} {MessageId} (solicitud {SolicitudId}): RabbitMQ no está configurado",
                SolicitudRegistrada.Tipo, mensaje.MessageId, mensaje.SolicitudId);
            return new ResultadoPublicacion(false, mensaje.MessageId, "RabbitMQ no está configurado.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(opciones.PublishTimeoutSeconds));

        try
        {
            var conn = await conexion.ObtenerAsync(timeout.Token);
            await using var canal = await conn.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                timeout.Token);
            await conexion.DeclararColasAsync(canal, timeout.Token);

            var propiedades = new BasicProperties
            {
                MessageId = mensaje.MessageId.ToString(),
                Type = SolicitudRegistrada.Tipo,
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                Persistent = true, // delivery_mode = 2: sobrevive a un reinicio del broker
                Timestamp = new AmqpTimestamp(new DateTimeOffset(mensaje.FechaEventoUtc).ToUnixTimeSeconds()),
                AppId = "PlataformaCreditos"
            };

            // Con confirmaciones habilitadas, BasicPublishAsync termina cuando el broker hace ack;
            // lanza PublishException si hace nack o si devuelve el mensaje (mandatory).
            await canal.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: opciones.QueueName,
                mandatory: true,
                basicProperties: propiedades,
                body: mensaje.Serializar(),
                cancellationToken: timeout.Token);

            logger.LogInformation("Publicado {Tipo} {MessageId} (solicitud {SolicitudId}) en {Cola}: confirmado por el broker",
                SolicitudRegistrada.Tipo, mensaje.MessageId, mensaje.SolicitudId, opciones.QueueName);
            return new ResultadoPublicacion(true, mensaje.MessageId);
        }
        catch (Exception ex) when (ex is PublishException or OperationCanceledException or BrokerUnreachableException
                                       or OperationInterruptedException or AlreadyClosedException or InvalidOperationException
                                       or IOException)
        {
            var motivo = ex switch
            {
                PublishException { IsReturn: true } => "el broker devolvió el mensaje (no enrutable)",
                PublishException => "el broker rechazó el mensaje (nack)",
                OperationCanceledException => $"sin confirmación del broker en {opciones.PublishTimeoutSeconds} s",
                _ => ex.Message
            };
            logger.LogError(ex,
                "FALLÓ la publicación de {Tipo} {MessageId} (solicitud {SolicitudId}, usuario {UsuarioId}): {Motivo}. " +
                "Reenviar manualmente con el mismo MessageId.",
                SolicitudRegistrada.Tipo, mensaje.MessageId, mensaje.SolicitudId, mensaje.UsuarioId, motivo);
            return new ResultadoPublicacion(false, mensaje.MessageId, motivo);
        }
    }
}
