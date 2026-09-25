using System.ComponentModel.DataAnnotations;
using PlataformaCreditos.Infrastructure;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.ViewModels;

public class FiltroSolicitudesViewModel : IValidatableObject
{
    public EstadoSolicitud? Estado { get; set; }

    [Display(Name = "Monto mínimo")]
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "El monto mínimo no puede ser negativo.")]
    public decimal? MontoMin { get; set; }

    [Display(Name = "Monto máximo")]
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "El monto máximo no puede ser negativo.")]
    public decimal? MontoMax { get; set; }

    [Display(Name = "Fecha desde")]
    [DataType(DataType.Date)]
    public DateTime? FechaInicio { get; set; }

    [Display(Name = "Fecha hasta")]
    [DataType(DataType.Date)]
    public DateTime? FechaFin { get; set; }

    public bool TieneFiltros =>
        Estado is not null || MontoMin is not null || MontoMax is not null || FechaInicio is not null || FechaFin is not null;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MontoMin is not null && MontoMax is not null && MontoMin > MontoMax)
        {
            yield return new ValidationResult(
                "Rango de montos inválido: el monto mínimo no puede ser mayor que el máximo.",
                [nameof(MontoMax)]);
        }

        if (FechaInicio is not null && FechaFin is not null && FechaInicio.Value.Date > FechaFin.Value.Date)
        {
            yield return new ValidationResult(
                "Rango de fechas inválido: la fecha de inicio no puede ser mayor que la fecha de fin.",
                [nameof(FechaFin)]);
        }
    }

    /// <summary>Aplica los filtros (fechas comparadas en hora de Lima, ambos extremos inclusivos).</summary>
    public IEnumerable<SolicitudResumen> Aplicar(IEnumerable<SolicitudResumen> solicitudes)
    {
        var resultado = solicitudes;
        if (Estado is not null)
        {
            resultado = resultado.Where(s => s.Estado == Estado);
        }
        if (MontoMin is not null)
        {
            resultado = resultado.Where(s => s.MontoSolicitado >= MontoMin);
        }
        if (MontoMax is not null)
        {
            resultado = resultado.Where(s => s.MontoSolicitado <= MontoMax);
        }
        if (FechaInicio is not null)
        {
            resultado = resultado.Where(s => Formato.ALocal(s.FechaSolicitud).Date >= FechaInicio.Value.Date);
        }
        if (FechaFin is not null)
        {
            resultado = resultado.Where(s => Formato.ALocal(s.FechaSolicitud).Date <= FechaFin.Value.Date);
        }
        return resultado;
    }
}
