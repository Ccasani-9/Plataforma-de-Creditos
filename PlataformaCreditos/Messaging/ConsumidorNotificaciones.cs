using System.Text;
using Microsoft.Extensions.Options;
using PlataformaCreditos.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PlataformaCreditos.Messaging;

/// <summary>
/// Consumidor de solicitudes.notificaciones (BackgroundService + RabbitMQ.Client, ACK manual):
/// - ACK solo después de guardar la notificación (o si el MessageId ya fue procesado).
/// - Mensaje inválido → BasicReject sin reencolar (va a la DLQ) + log.
/// - Error de procesamiento → no se confirma; un único reintento (requeue) y luego DLQ: sin reintentos infinitos.
/// Se desactiva con RabbitMq__ConsumerEnabled=false.
/// </summary>
public class ConsumidorNotificaciones(
    RabbitMqConexion conexion,
    IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> opciones,
    ILogger<ConsumidorNotificaciones> logger) : BackgroundService
{
    private readonly RabbitMqOptions _opciones = opciones.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.ConsumerEnabled)
        {
            logger.LogWarning("Consumidor de {Cola} DESACTIVADO (RabbitMq__ConsumerEnabled=false): los mensajes quedarán en la cola",
                _opciones.QueueName);
            return;
        }

        if (!_opciones.Configurado)
        {
            logger.LogWarning("Consumidor de {Cola} no iniciado: RabbitMq__ConnectionString no está configurada", _opciones.QueueName);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumirAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Consumidor de {Cola} detenido por un error; reintentando en 15 s", _opciones.QueueName);
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }

    private async Task ConsumirAsync(CancellationToken stoppingToken)
    {
        var conn = await conexion.ObtenerAsync(stoppingToken);
        await using var canal = await conn.CreateChannelAsync(cancellationToken: stoppingToken);
        await conexion.DeclararColasAsync(canal, stoppingToken);
        await canal.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, stoppingToken);

        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += (_, entrega) => ProcesarEntregaAsync(canal, entrega, stoppingToken);

        var tag = await canal.BasicConsumeAsync(_opciones.QueueName, autoAck: false, consumidor, stoppingToken);
        logger.LogInformation("Consumidor escuchando {Cola} (ACK manual, prefetch 1, tag {Tag})", _opciones.QueueName, tag);

        // Mantiene vivo el canal; la recuperación automática del cliente se encarga de los cortes de red.
        var cerrado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        canal.ChannelShutdownAsync += (_, args) =>
        {
            if (args.Initiator != ShutdownInitiator.Application)
            {
                logger.LogWarning("Canal del consumidor cerrado: {Motivo}", args.ReplyText);
                // Si la conexión sigue abierta, el canal no se recupera solo: se reinicia el ciclo de consumo.
                if (conn.IsOpen)
                {
                    cerrado.TrySetException(new InvalidOperationException("Canal cerrado por el broker: " + args.ReplyText));
                }
            }
            return Task.CompletedTask;
        };
        await using var registro = stoppingToken.Register(() => cerrado.TrySetResult());
        await cerrado.Task;
    }

    private async Task ProcesarEntregaAsync(IChannel canal, BasicDeliverEventArgs entrega, CancellationToken ct)
    {
        var mensaje = SolicitudRegistrada.Deserializar(entrega.Body.Span, out var error);
        if (mensaje is null)
        {
            logger.LogError("Mensaje INVÁLIDO rechazado sin reencolar (→ {Dlq}). MessageId={MessageId} Motivo={Motivo} Cuerpo={Cuerpo}",
                _opciones.DeadLetterQueueName, entrega.BasicProperties.MessageId, error, Resumir(entrega.Body));
            await canal.BasicRejectAsync(entrega.DeliveryTag, requeue: false, ct);
            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var servicio = scope.ServiceProvider.GetRequiredService<NotificacionesService>();
            var resultado = await servicio.ProcesarAsync(mensaje, ct);

            switch (resultado)
            {
                case ResultadoProcesamiento.Insertada:
                    await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, ct);
                    logger.LogInformation("Notificación guardada y ACK enviado: MessageId={MessageId} solicitud {SolicitudId}",
                        mensaje.MessageId, mensaje.SolicitudId);
                    break;

                case ResultadoProcesamiento.Duplicada:
                    await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, ct);
                    logger.LogInformation("MessageId={MessageId} ya fue procesado: ACK sin insertar (sin duplicados)", mensaje.MessageId);
                    break;

                default:
                    logger.LogError("Mensaje INVÁLIDO rechazado sin reencolar (→ {Dlq}). MessageId={MessageId}",
                        _opciones.DeadLetterQueueName, mensaje.MessageId);
                    await canal.BasicRejectAsync(entrega.DeliveryTag, requeue: false, ct);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // No se confirma como exitoso. Un solo reintento; si vuelve a fallar va a la DLQ.
            var reencolar = !entrega.Redelivered;
            logger.LogError(ex, "Error procesando MessageId={MessageId}; NACK {Accion}", mensaje.MessageId,
                reencolar ? "con reencolado (1 reintento)" : $"sin reencolar (→ {_opciones.DeadLetterQueueName}, requiere reenvío manual)");
            await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: reencolar, CancellationToken.None);
        }
    }

    private static string Resumir(ReadOnlyMemory<byte> cuerpo)
    {
        var texto = Encoding.UTF8.GetString(cuerpo.Span);
        return texto.Length <= 300 ? texto : texto[..300] + "…";
    }
}
