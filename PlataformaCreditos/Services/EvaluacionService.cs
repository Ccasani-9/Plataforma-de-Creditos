using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Hubs;
using PlataformaCreditos.Models;
using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Services;

public record ResultadoEvaluacion(bool Exito, string Mensaje, SolicitudCredito? Solicitud = null, string? UsuarioPropietarioId = null)
{
    public static ResultadoEvaluacion Error(string mensaje) => new(false, mensaje);
}

/// <summary>Evaluación de solicitudes por parte del rol Analista.</summary>
public class EvaluacionService(
    ApplicationDbContext db,
    SolicitudesCache cache,
    NotificadorSolicitudes notificador,
    ILogger<EvaluacionService> logger)
{
    public async Task<IReadOnlyList<SolicitudPendienteViewModel>> ObtenerPendientesAsync()
    {
        return await (
                from s in db.SolicitudesCredito.AsNoTracking()
                join u in db.Users on s.Cliente!.UsuarioId equals u.Id
                where s.Estado == EstadoSolicitud.Pendiente
                orderby s.FechaSolicitud
                select new SolicitudPendienteViewModel(
                    s.Id, u.Email ?? u.UserName ?? u.Id, s.Cliente!.IngresosMensuales, s.Cliente.Activo, s.MontoSolicitado, s.FechaSolicitud))
            .ToListAsync();
    }

    public Task<ResultadoEvaluacion> AprobarAsync(int solicitudId) =>
        ProcesarAsync(solicitudId, s => s.Aprobar(s.Cliente!.IngresosMensuales), "aprobada");

    public Task<ResultadoEvaluacion> RechazarAsync(int solicitudId, string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Task.FromResult(ResultadoEvaluacion.Error("El motivo de rechazo es obligatorio."));
        }

        if (motivo.Trim().Length > ReglasCredito.LongitudMaximaMotivo)
        {
            return Task.FromResult(ResultadoEvaluacion.Error($"El motivo no puede superar {ReglasCredito.LongitudMaximaMotivo} caracteres."));
        }

        return ProcesarAsync(solicitudId, s => s.Rechazar(motivo), "rechazada");
    }

    /// <summary>
    /// 1) Aplica la regla de dominio, 2) guarda en la base de datos (con control de concurrencia
    /// sobre Estado), 3) invalida la caché Redis del propietario y 4) solo entonces emite el
    /// evento WebSocket SolicitudEstadoActualizado al propietario.
    /// </summary>
    private async Task<ResultadoEvaluacion> ProcesarAsync(int solicitudId, Action<SolicitudCredito> accion, string verbo)
    {
        var solicitud = await db.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == solicitudId);
        if (solicitud is null)
        {
            return ResultadoEvaluacion.Error($"La solicitud #{solicitudId} no existe.");
        }

        try
        {
            accion(solicitud);
            await db.SaveChangesAsync();
        }
        catch (ReglaNegocioException ex)
        {
            return ResultadoEvaluacion.Error(ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otro analista la procesó entre la lectura y la escritura (UPDATE ... WHERE Estado = 'Pendiente').
            return ResultadoEvaluacion.Error($"La solicitud #{solicitudId} ya fue procesada por otro analista.");
        }
        catch (DbUpdateException ex)
        {
            // Última barrera: triggers/CHECK de SQLite (p. ej. monto > 5x ingresos).
            logger.LogWarning(ex, "La base de datos rechazó la evaluación de la solicitud {SolicitudId}", solicitudId);
            return ResultadoEvaluacion.Error($"No se pudo procesar la solicitud #{solicitudId}: {ex.GetBaseException().Message}");
        }

        var propietarioId = solicitud.Cliente!.UsuarioId;
        await cache.InvalidarAsync(propietarioId);
        logger.LogInformation("Solicitud {SolicitudId} {Verbo} (propietario {UsuarioId})", solicitud.Id, verbo, propietarioId);

        // El destinatario sale de la base de datos (Cliente.UsuarioId), nunca del navegador.
        await notificador.NotificarEstadoAsync(propietarioId, solicitud);

        return new ResultadoEvaluacion(true, $"Solicitud #{solicitud.Id} {verbo} correctamente.", solicitud, propietarioId);
    }
}
