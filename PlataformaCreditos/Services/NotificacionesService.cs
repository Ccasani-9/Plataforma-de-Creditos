using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Messaging;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Services;

public enum ResultadoProcesamiento
{
    Insertada,
    Duplicada,
    Invalida
}

public class NotificacionesService(ApplicationDbContext db, ILogger<NotificacionesService> logger)
{
    public async Task<IReadOnlyList<Notificacion>> ObtenerMisNotificacionesAsync(string usuarioId) =>
        await db.Notificaciones
            .AsNoTracking()
            .Where(n => n.UsuarioId == usuarioId)
            .OrderByDescending(n => n.FechaProcesamientoUtc)
            .ToListAsync();

    /// <summary>
    /// Guarda la notificación de un mensaje SolicitudRegistrada de forma idempotente.
    /// Solo notifica (no aprueba ni rechaza créditos).
    /// </summary>
    public async Task<ResultadoProcesamiento> ProcesarAsync(SolicitudRegistrada mensaje, CancellationToken ct)
    {
        if (await db.Notificaciones.AnyAsync(n => n.MessageId == mensaje.MessageId, ct))
        {
            return ResultadoProcesamiento.Duplicada;
        }

        // La solicitud debe existir y pertenecer al UsuarioId del mensaje.
        var propietario = await db.SolicitudesCredito
            .Where(s => s.Id == mensaje.SolicitudId)
            .Select(s => s.Cliente!.UsuarioId)
            .FirstOrDefaultAsync(ct);
        if (propietario is null || propietario != mensaje.UsuarioId)
        {
            logger.LogError("Mensaje {MessageId} inválido: la solicitud {SolicitudId} no existe o no pertenece al usuario {UsuarioId}",
                mensaje.MessageId, mensaje.SolicitudId, mensaje.UsuarioId);
            return ResultadoProcesamiento.Invalida;
        }

        db.Notificaciones.Add(new Notificacion
        {
            MessageId = mensaje.MessageId,
            SolicitudId = mensaje.SolicitudId,
            UsuarioId = mensaje.UsuarioId,
            Texto = Notificacion.TextoSolicitudRegistrada,
            FechaProcesamientoUtc = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(ct);
            return ResultadoProcesamiento.Insertada;
        }
        catch (DbUpdateException)
        {
            // Carrera con otra entrega del mismo mensaje: el índice único de MessageId evitó el duplicado.
            if (await db.Notificaciones.AsNoTracking().AnyAsync(n => n.MessageId == mensaje.MessageId, ct))
            {
                return ResultadoProcesamiento.Duplicada;
            }
            throw;
        }
    }
}
