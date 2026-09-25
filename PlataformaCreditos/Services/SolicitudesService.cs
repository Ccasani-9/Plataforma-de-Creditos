using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Infrastructure;
using PlataformaCreditos.Models;
using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Services;

public record ResultadoRegistro(bool Exito, string Mensaje, SolicitudCredito? Solicitud = null)
{
    public static ResultadoRegistro Error(string mensaje) => new(false, mensaje);
}

public class SolicitudesService(ApplicationDbContext db, SolicitudesCache cache, ILogger<SolicitudesService> logger)
{
    public Task<Cliente?> ObtenerClienteAsync(string usuarioId) =>
        db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

    public Task<bool> TienePendienteAsync(int clienteId) =>
        db.SolicitudesCredito.AnyAsync(s => s.ClienteId == clienteId && s.Estado == EstadoSolicitud.Pendiente);

    /// <summary>
    /// Todas las solicitudes del usuario, más recientes primero. Se sirven desde Redis
    /// durante 60 segundos; los filtros se aplican sobre la lista cacheada.
    /// </summary>
    public async Task<(IReadOnlyList<SolicitudResumen> Solicitudes, bool DesdeCache)> ObtenerMisSolicitudesAsync(string usuarioId)
    {
        var cacheadas = await cache.ObtenerAsync(usuarioId);
        if (cacheadas is not null)
        {
            return (cacheadas, true);
        }

        var solicitudes = await ConsultarMisSolicitudesAsync(usuarioId);
        await cache.GuardarAsync(usuarioId, solicitudes);
        return (solicitudes, false);
    }

    /// <summary>Estado vigente leído directamente de la base de datos (sin caché), p. ej. al reconectar el WebSocket.</summary>
    public Task<IReadOnlyList<SolicitudResumen>> ObtenerEstadosVigentesAsync(string usuarioId) =>
        ConsultarMisSolicitudesAsync(usuarioId);

    private async Task<IReadOnlyList<SolicitudResumen>> ConsultarMisSolicitudesAsync(string usuarioId)
    {
        return await db.SolicitudesCredito
            .AsNoTracking()
            .Where(s => s.Cliente!.UsuarioId == usuarioId)
            .OrderByDescending(s => s.FechaSolicitud)
            .ThenByDescending(s => s.Id)
            .Select(s => new SolicitudResumen(s.Id, s.MontoSolicitado, s.FechaSolicitud, s.Estado, s.MotivoRechazo))
            .ToListAsync();
    }

    /// <summary>Detalle de una solicitud solo si pertenece al usuario indicado.</summary>
    public Task<SolicitudCredito?> ObtenerDetalleAsync(int id, string usuarioId) =>
        db.SolicitudesCredito
            .AsNoTracking()
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id && s.Cliente!.UsuarioId == usuarioId);

    /// <summary>
    /// Registra una solicitud Pendiente aplicando todas las reglas de negocio en el servidor.
    /// El usuario se obtiene de la sesión autenticada, nunca del formulario.
    /// </summary>
    public async Task<ResultadoRegistro> RegistrarAsync(string usuarioId, decimal montoSolicitado)
    {
        if (montoSolicitado <= 0)
        {
            return ResultadoRegistro.Error("El monto solicitado debe ser mayor a 0.");
        }

        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);
        if (cliente is null)
        {
            return ResultadoRegistro.Error("Tu usuario no tiene un perfil de cliente. Regístralo antes de solicitar un crédito.");
        }

        if (!cliente.Activo)
        {
            return ResultadoRegistro.Error("Tu perfil de cliente está inactivo; no puedes registrar solicitudes.");
        }

        if (await TienePendienteAsync(cliente.Id))
        {
            return ResultadoRegistro.Error("Ya tienes una solicitud Pendiente. Espera su evaluación antes de registrar otra.");
        }

        if (montoSolicitado > cliente.MontoMaximoRegistrable)
        {
            return ResultadoRegistro.Error(
                $"El monto solicitado ({Formato.Soles(montoSolicitado)}) no puede superar {ReglasCredito.MultiploMaximoRegistro} veces " +
                $"tus ingresos mensuales (máximo {Formato.Soles(cliente.MontoMaximoRegistrable)}).");
        }

        var solicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = montoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente
        };
        db.SolicitudesCredito.Add(solicitud);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Carrera entre dos envíos simultáneos: el índice único filtrado garantiza una sola Pendiente.
            logger.LogWarning(ex, "No se pudo registrar la solicitud del usuario {UsuarioId}", usuarioId);
            db.Entry(solicitud).State = EntityState.Detached;
            return ResultadoRegistro.Error("No se pudo registrar la solicitud: ya existe una solicitud Pendiente o los datos no son válidos.");
        }

        logger.LogInformation("Solicitud {SolicitudId} registrada por {UsuarioId} por {Monto}", solicitud.Id, usuarioId, montoSolicitado);
        await cache.InvalidarAsync(usuarioId);
        return new ResultadoRegistro(true, $"Solicitud #{solicitud.Id} registrada correctamente por {Formato.Soles(montoSolicitado)}. Estado: Pendiente.", solicitud);
    }

    /// <summary>Crea el perfil de cliente (activo) para un usuario recién registrado.</summary>
    public async Task<ResultadoRegistro> CrearPerfilAsync(string usuarioId, decimal ingresosMensuales)
    {
        if (ingresosMensuales <= 0)
        {
            return ResultadoRegistro.Error("Los ingresos mensuales deben ser mayores a 0.");
        }

        if (await db.Clientes.AnyAsync(c => c.UsuarioId == usuarioId))
        {
            return ResultadoRegistro.Error("Ya tienes un perfil de cliente.");
        }

        db.Clientes.Add(new Cliente { UsuarioId = usuarioId, IngresosMensuales = ingresosMensuales, Activo = true });
        await db.SaveChangesAsync();
        return new ResultadoRegistro(true, "Perfil de cliente creado correctamente.");
    }
}
