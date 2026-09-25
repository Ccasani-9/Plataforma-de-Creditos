using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.ViewModels;

public class PerfilClienteViewModel
{
    [Required(ErrorMessage = "Ingresa tus ingresos mensuales.")]
    [Display(Name = "Ingresos mensuales (S/)")]
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "Los ingresos mensuales deben ser mayores a 0.")]
    public decimal? IngresosMensuales { get; set; }
}
