using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.ViewModels;

public class ReenvioMensajeViewModel
{
    [Required(ErrorMessage = "Ingresa el MessageId original.")]
    [Display(Name = "MessageId (UUID)")]
    public Guid? MessageId { get; set; }

    [Required(ErrorMessage = "Ingresa el número de solicitud.")]
    [Display(Name = "Solicitud #")]
    [Range(1, int.MaxValue, ErrorMessage = "Número de solicitud inválido.")]
    public int? SolicitudId { get; set; }
}
