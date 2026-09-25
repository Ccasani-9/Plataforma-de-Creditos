using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Services;

public class SolicitudesService(ApplicationDbContext db)
{
    public Task<Cliente?> ObtenerClienteAsync(string usuarioId) =>
        db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

    /// <summary>Todas las solicitudes del usuario, más recientes primero.</summary>
    public async Task<IReadOnlyList<SolicitudResumen>> ObtenerMisSolicitudesAsync(string usuarioId)
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
}
