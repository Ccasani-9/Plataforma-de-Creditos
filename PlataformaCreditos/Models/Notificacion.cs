namespace PlataformaCreditos.Models;

/// <summary>Notificación generada por el consumidor de la cola solicitudes.notificaciones.</summary>
public class Notificacion
{
    public const string TextoSolicitudRegistrada = "Recibimos tu solicitud de crédito y está pendiente de evaluación";

    public int Id { get; set; }

    /// <summary>Id del mensaje en la cola; único para que una redelivery no genere duplicados.</summary>
    public Guid MessageId { get; set; }

    public int SolicitudId { get; set; }

    public string UsuarioId { get; set; } = string.Empty;

    public string Texto { get; set; } = string.Empty;

    public DateTime FechaProcesamientoUtc { get; set; }
}
