using System.ComponentModel.DataAnnotations;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.ViewModels;

public class RegistrarSolicitudViewModel
{
    [Required(ErrorMessage = "Ingresa el monto solicitado.")]
    [Display(Name = "Monto solicitado (S/)")]
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    public decimal? MontoSolicitado { get; set; }

    // Información de contexto (solo lectura, se recalcula en el servidor en cada request).
    public Cliente? Cliente { get; set; }

    public bool TienePendiente { get; set; }

    public string? MensajeExito { get; set; }

    public int? SolicitudCreadaId { get; set; }

    public Guid? MessageId { get; set; }

    public string? AdvertenciaNotificacion { get; set; }
}
