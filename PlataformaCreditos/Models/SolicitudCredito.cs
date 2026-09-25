using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models;

public class SolicitudCredito
{
    public int Id { get; set; }

    public int ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    [Display(Name = "Monto solicitado")]
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [DataType(DataType.Currency)]
    public decimal MontoSolicitado { get; set; }

    [Display(Name = "Fecha de solicitud")]
    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    [Display(Name = "Motivo de rechazo")]
    [StringLength(ReglasCredito.LongitudMaximaMotivo)]
    public string? MotivoRechazo { get; set; }

    /// <summary>
    /// Regla de dominio: solo se aprueba una solicitud Pendiente cuyo monto no supere
    /// 5 veces los ingresos mensuales del cliente.
    /// </summary>
    public void Aprobar(decimal ingresosMensuales)
    {
        AsegurarPendiente();
        if (MontoSolicitado > ingresosMensuales * ReglasCredito.MultiploMaximoAprobacion)
        {
            throw new ReglaNegocioException(
                $"No se puede aprobar: el monto ({MontoSolicitado:N2}) supera {ReglasCredito.MultiploMaximoAprobacion} veces los ingresos mensuales ({ingresosMensuales:N2}).");
        }

        Estado = EstadoSolicitud.Aprobado;
        MotivoRechazo = null;
    }

    public void Rechazar(string? motivo)
    {
        AsegurarPendiente();
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ReglaNegocioException("El motivo de rechazo es obligatorio.");
        }

        Estado = EstadoSolicitud.Rechazado;
        MotivoRechazo = motivo.Trim();
    }

    private void AsegurarPendiente()
    {
        if (Estado != EstadoSolicitud.Pendiente)
        {
            throw new ReglaNegocioException($"La solicitud #{Id} ya fue procesada (estado actual: {Estado}).");
        }
    }
}
