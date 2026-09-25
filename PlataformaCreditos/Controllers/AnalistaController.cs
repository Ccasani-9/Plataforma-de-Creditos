using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Messaging;
using PlataformaCreditos.Models;
using PlataformaCreditos.Services;
using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Controllers;

/// <summary>Panel /Analista: solo usuarios con rol Analista (el resto recibe "Acceso denegado").</summary>
[Authorize(Roles = Roles.Analista)]
public class AnalistaController(EvaluacionService evaluacion, PublicadorSolicitudes publicador, ApplicationDbContext db) : Controller
{
    // GET /Analista
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await evaluacion.ObtenerPendientesAsync());
    }

    // POST /Analista/Aprobar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var resultado = await evaluacion.AprobarAsync(id);
        return Resultado(resultado);
    }

    // POST /Analista/Rechazar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string? motivoRechazo)
    {
        var resultado = await evaluacion.RechazarAsync(id, motivoRechazo);
        return Resultado(resultado);
    }

    // GET /Analista/Reenviar  —  reenvío manual a Cloud MQ con el mismo MessageId
    [HttpGet]
    public IActionResult Reenviar() => View(new ReenvioMensajeViewModel());

    // POST /Analista/Reenviar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reenviar(ReenvioMensajeViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // El destinatario se toma de la base de datos, no del formulario.
        var usuarioId = await db.SolicitudesCredito
            .Where(s => s.Id == modelo.SolicitudId)
            .Select(s => s.Cliente!.UsuarioId)
            .FirstOrDefaultAsync();
        if (usuarioId is null)
        {
            ModelState.AddModelError(string.Empty, $"La solicitud #{modelo.SolicitudId} no existe.");
            return View(modelo);
        }

        var resultado = await publicador.PublicarAsync(
            new SolicitudRegistrada(modelo.MessageId!.Value, modelo.SolicitudId!.Value, usuarioId, DateTime.UtcNow));

        TempData[resultado.Exito ? "Exito" : "Error"] = resultado.Exito
            ? $"Mensaje {resultado.MessageId} reenviado y confirmado por el broker."
            : $"No se pudo reenviar el mensaje {resultado.MessageId}: {resultado.Error}";
        return RedirectToAction(nameof(Reenviar));
    }

    private RedirectToActionResult Resultado(ResultadoEvaluacion resultado)
    {
        TempData[resultado.Exito ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }
}
