using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlataformaCreditos.Models;
using PlataformaCreditos.Services;

namespace PlataformaCreditos.Controllers;

/// <summary>Panel /Analista: solo usuarios con rol Analista (el resto recibe "Acceso denegado").</summary>
[Authorize(Roles = Roles.Analista)]
public class AnalistaController(EvaluacionService evaluacion) : Controller
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

    private RedirectToActionResult Resultado(ResultadoEvaluacion resultado)
    {
        TempData[resultado.Exito ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }
}
