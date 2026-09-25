namespace PlataformaCreditos.Messaging;

/// <summary>Sección "RabbitMq" (variables RabbitMq__ConnectionString, RabbitMq__QueueName, RabbitMq__ConsumerEnabled).</summary>
public class RabbitMqOptions
{
    public const string Seccion = "RabbitMq";

    /// <summary>URI del broker. En producción debe ser amqps:// (CloudAMQP).</summary>
    public string? ConnectionString { get; set; }

    public string QueueName { get; set; } = "solicitudes.notificaciones";

    public bool ConsumerEnabled { get; set; } = true;

    /// <summary>Tiempo máximo para que el broker confirme una publicación.</summary>
    public int PublishTimeoutSeconds { get; set; } = 10;

    public bool Configurado => !string.IsNullOrWhiteSpace(ConnectionString);

    /// <summary>Cola a la que van (dead-letter) los mensajes inválidos o que fallaron tras un reintento.</summary>
    public string DeadLetterQueueName => QueueName + ".dlq";
}
