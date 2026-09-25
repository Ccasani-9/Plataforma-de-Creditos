using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlataformaCreditos.Services;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class NotificacionesController(NotificacionesService notificaciones) : Controller
{
    // GET /Notificaciones  —  "Mis notificaciones" (solo las del usuario autenticado)
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return View(await notificaciones.ObtenerMisNotificacionesAsync(usuarioId));
    }
}
