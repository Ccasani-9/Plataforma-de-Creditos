using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlataformaCreditos.Infrastructure;
using PlataformaCreditos.Services;
using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudesController(SolicitudesService solicitudes) : Controller
{
    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET /Solicitudes  —  "Mis solicitudes"
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] FiltroSolicitudesViewModel filtro)
    {
        var cliente = await solicitudes.ObtenerClienteAsync(UsuarioId);
        var (todas, desdeCache) = await solicitudes.ObtenerMisSolicitudesAsync(UsuarioId);

        // Validación server-side: si los filtros son inválidos no se aplican y se muestran los errores.
        var visibles = ModelState.IsValid ? filtro.Aplicar(todas).ToList() : todas.ToList();

        return View(new MisSolicitudesViewModel
        {
            Filtro = filtro,
            Solicitudes = visibles,
            TotalSinFiltrar = todas.Count,
            TienePerfilCliente = cliente is not null,
            DesdeCache = desdeCache
        });
    }

    // GET /Solicitudes/Detalle/5
    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var solicitud = await solicitudes.ObtenerDetalleAsync(id, UsuarioId);
        if (solicitud is null)
        {
            // No se distingue entre "no existe" y "es de otro usuario" para no filtrar información.
            return NotFound();
        }

        // Sesión (Redis): recordar la última solicitud visitada para el enlace del layout.
        UltimaSolicitudSesion.Guardar(HttpContext.Session,
            new UltimaSolicitudVisitada(UsuarioId, solicitud.Id, solicitud.MontoSolicitado));

        return View(solicitud);
    }

    // GET /Solicitudes/Registrar
    [HttpGet]
    public async Task<IActionResult> Registrar()
    {
        var modelo = new RegistrarSolicitudViewModel();
        await CargarContextoAsync(modelo);
        return View(modelo);
    }

    // POST /Solicitudes/Registrar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(RegistrarSolicitudViewModel modelo)
    {
        if (ModelState.IsValid)
        {
            var resultado = await solicitudes.RegistrarAsync(UsuarioId, modelo.MontoSolicitado!.Value);
            if (resultado.Exito)
            {
                // Feedback en la misma vista: se limpia el formulario y se muestra el mensaje de éxito.
                ModelState.Clear();
                modelo = new RegistrarSolicitudViewModel
                {
                    MensajeExito = resultado.Mensaje,
                    SolicitudCreadaId = resultado.Solicitud!.Id,
                    MessageId = resultado.MessageId,
                    AdvertenciaNotificacion = resultado.AdvertenciaNotificacion
                };
            }
            else
            {
                ModelState.AddModelError(string.Empty, resultado.Mensaje);
            }
        }

        await CargarContextoAsync(modelo);
        return View(modelo);
    }

    // GET /Solicitudes/Perfil
    [HttpGet]
    public async Task<IActionResult> Perfil()
    {
        if (await solicitudes.ObtenerClienteAsync(UsuarioId) is not null)
        {
            return RedirectToAction(nameof(Registrar));
        }

        return View(new PerfilClienteViewModel());
    }

    // POST /Solicitudes/Perfil
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Perfil(PerfilClienteViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var resultado = await solicitudes.CrearPerfilAsync(UsuarioId, modelo.IngresosMensuales!.Value);
        if (!resultado.Exito)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje);
            return View(modelo);
        }

        TempData["Exito"] = resultado.Mensaje;
        return RedirectToAction(nameof(Registrar));
    }

    private async Task CargarContextoAsync(RegistrarSolicitudViewModel modelo)
    {
        modelo.Cliente = await solicitudes.ObtenerClienteAsync(UsuarioId);
        modelo.TienePendiente = modelo.Cliente is not null && await solicitudes.TienePendienteAsync(modelo.Cliente.Id);
    }
}
