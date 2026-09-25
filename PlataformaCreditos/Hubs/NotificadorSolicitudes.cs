using Microsoft.AspNetCore.SignalR;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Hubs;

/// <summary>Emite eventos en tiempo real únicamente a las conexiones del usuario propietario.</summary>
public class NotificadorSolicitudes(IHubContext<SolicitudesHub, ISolicitudesCliente> hub, ILogger<NotificadorSolicitudes> logger)
{
    public async Task NotificarEstadoAsync(string usuarioPropietarioId, SolicitudCredito solicitud)
    {
        var evento = new SolicitudEstadoActualizado(solicitud.Id, solicitud.Estado.ToString(), solicitud.MotivoRechazo);
        try
        {
            // Clients.User usa el NameIdentifier de Identity: solo lo reciben las pestañas del propietario.
            await hub.Clients.User(usuarioPropietarioId).SolicitudEstadoActualizado(evento);
            logger.LogInformation("Evento SolicitudEstadoActualizado enviado: solicitud {SolicitudId} → usuario {UsuarioId} ({Estado})",
                solicitud.Id, usuarioPropietarioId, evento.Estado);
        }
        catch (Exception ex)
        {
            // El estado ya está guardado; si el cliente está desconectado lo recupera al reconectar.
            logger.LogError(ex, "No se pudo emitir SolicitudEstadoActualizado para la solicitud {SolicitudId}", solicitud.Id);
        }
    }
}
