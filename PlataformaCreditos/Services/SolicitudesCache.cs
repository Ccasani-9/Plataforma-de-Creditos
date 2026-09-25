using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Services;

/// <summary>
/// Caché (Redis) del listado de solicitudes por usuario, con expiración absoluta de 60 segundos.
/// Si Redis falla, se registra el error y se continúa contra la base de datos.
/// </summary>
public class SolicitudesCache(IDistributedCache cache, ILogger<SolicitudesCache> logger)
{
    public static readonly TimeSpan Duracion = TimeSpan.FromSeconds(60);

    public static string Clave(string usuarioId) => $"solicitudes:usuario:{usuarioId}";

    public async Task<IReadOnlyList<SolicitudResumen>?> ObtenerAsync(string usuarioId)
    {
        try
        {
            var json = await cache.GetStringAsync(Clave(usuarioId));
            if (json is null)
            {
                logger.LogInformation("Cache MISS {Clave}", Clave(usuarioId));
                return null;
            }

            logger.LogInformation("Cache HIT {Clave}", Clave(usuarioId));
            return JsonSerializer.Deserialize<List<SolicitudResumen>>(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error leyendo la caché {Clave}; se consulta la base de datos", Clave(usuarioId));
            return null;
        }
    }

    public async Task GuardarAsync(string usuarioId, IReadOnlyList<SolicitudResumen> solicitudes)
    {
        try
        {
            await cache.SetStringAsync(
                Clave(usuarioId),
                JsonSerializer.Serialize(solicitudes),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Duracion });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error guardando la caché {Clave}", Clave(usuarioId));
        }
    }

    public async Task InvalidarAsync(string usuarioId)
    {
        try
        {
            await cache.RemoveAsync(Clave(usuarioId));
            logger.LogInformation("Cache invalidada {Clave}", Clave(usuarioId));
        }
        catch (Exception ex)
        {
            // La entrada expira sola en 60s como máximo; se deja evidencia del fallo.
            logger.LogError(ex, "Error invalidando la caché {Clave}", Clave(usuarioId));
        }
    }
}
