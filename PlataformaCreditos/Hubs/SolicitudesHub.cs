using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PlataformaCreditos.Services;

namespace PlataformaCreditos.Hubs;

/// <summary>Evento enviado al propietario cuando un analista aprueba o rechaza su solicitud.</summary>
public record SolicitudEstadoActualizado(int SolicitudId, string Estado, string? MotivoRechazo);

/// <summary>Métodos que el servidor invoca en el navegador (cliente fuertemente tipado).</summary>
public interface ISolicitudesCliente
{
    Task SolicitudEstadoActualizado(SolicitudEstadoActualizado evento);
}

/// <summary>
/// Hub en /hubs/solicitudes. Requiere un usuario autenticado con Identity (las conexiones
/// anónimas se rechazan con 401). La identidad se toma siempre de la cookie en el servidor
/// (<see cref="HubCallerContext.UserIdentifier"/>), nunca de datos enviados por el navegador.
/// </summary>
[Authorize]
public class SolicitudesHub(SolicitudesService solicitudes, ILogger<SolicitudesHub> logger) : Hub<ISolicitudesCliente>
{
    public const string Ruta = "/hubs/solicitudes";

    public override Task OnConnectedAsync()
    {
        logger.LogInformation("WebSocket conectado: usuario {UsuarioId}, conexión {ConnectionId}", Context.UserIdentifier, Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("WebSocket desconectado: usuario {UsuarioId}, conexión {ConnectionId}", Context.UserIdentifier, Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Estado vigente (desde la base de datos) de las solicitudes del usuario conectado.
    /// El navegador lo invoca al reconectar para recuperar cambios ocurridos durante la desconexión.
    /// </summary>
    public async Task<IReadOnlyList<SolicitudEstadoActualizado>> ObtenerEstadoActual()
    {
        var usuarioId = Context.UserIdentifier ?? throw new HubException("Usuario no autenticado.");
        var vigentes = await solicitudes.ObtenerEstadosVigentesAsync(usuarioId);
        return vigentes.Select(s => new SolicitudEstadoActualizado(s.Id, s.Estado.ToString(), s.MotivoRechazo)).ToList();
    }
}
