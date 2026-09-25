using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace PlataformaCreditos.Messaging;

/// <summary>
/// Conexión única (y perezosa) al broker, compartida por el productor y el consumidor.
/// Cada uno abre sus propios canales. Usa recuperación automática ante cortes de red.
/// </summary>
public sealed class RabbitMqConexion(IOptions<RabbitMqOptions> opciones, IHostEnvironment env, ILogger<RabbitMqConexion> logger)
    : IAsyncDisposable
{
    private readonly RabbitMqOptions _opciones = opciones.Value;
    private readonly SemaphoreSlim _candado = new(1, 1);
    private IConnection? _conexion;

    public RabbitMqOptions Opciones => _opciones;

    public async Task<IConnection> ObtenerAsync(CancellationToken ct = default)
    {
        if (_conexion is { IsOpen: true })
        {
            return _conexion;
        }

        await _candado.WaitAsync(ct);
        try
        {
            if (_conexion is { IsOpen: true })
            {
                return _conexion;
            }

            if (!_opciones.Configurado)
            {
                throw new InvalidOperationException("RabbitMq__ConnectionString no está configurada.");
            }

            var factory = new ConnectionFactory
            {
                Uri = ObtenerUri(),
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                ClientProvidedName = $"PlataformaCreditos ({env.EnvironmentName})"
            };

            if (_conexion is not null)
            {
                await _conexion.DisposeAsync();
            }

            _conexion = await factory.CreateConnectionAsync(ct);
            logger.LogInformation("Conectado a RabbitMQ {Host} ({Protocolo})", factory.HostName, factory.Ssl.Enabled ? "AMQPS/TLS" : "AMQP");
            return _conexion;
        }
        finally
        {
            _candado.Release();
        }
    }

    /// <summary>Declara la cola durable y su cola de mensajes fallidos (idempotente).</summary>
    public async Task DeclararColasAsync(IChannel canal, CancellationToken ct = default)
    {
        await canal.QueueDeclareAsync(_opciones.DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: ct);

        await canal.QueueDeclareAsync(_opciones.QueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                // Los mensajes rechazados sin reencolar quedan en la DLQ para revisión y reenvío manual.
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = _opciones.DeadLetterQueueName
            },
            cancellationToken: ct);
    }

    /// <summary>En producción se exige AMQPS (TLS): un URI amqp:// se eleva a amqps:// (puerto 5671).</summary>
    private Uri ObtenerUri()
    {
        var uri = new Uri(_opciones.ConnectionString!);
        if (uri.Scheme == "amqp" && !env.IsDevelopment())
        {
            var seguro = new UriBuilder(uri) { Scheme = "amqps", Port = uri.IsDefaultPort || uri.Port == 5672 ? 5671 : uri.Port };
            logger.LogWarning("RabbitMq__ConnectionString usa amqp://; se fuerza AMQPS en {Entorno}", env.EnvironmentName);
            return seguro.Uri;
        }

        return uri;
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexion is not null)
        {
            await _conexion.DisposeAsync();
        }
        _candado.Dispose();
    }
}
