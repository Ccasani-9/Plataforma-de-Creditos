using PlataformaCreditos.Models;

namespace PlataformaCreditos.ViewModels;

/// <summary>Proyección ligera (y serializable) de una solicitud para listados.</summary>
public record SolicitudResumen(
    int Id,
    decimal MontoSolicitado,
    DateTime FechaSolicitud,
    EstadoSolicitud Estado,
    string? MotivoRechazo);
