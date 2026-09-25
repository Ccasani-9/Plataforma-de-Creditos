using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        var todas = await solicitudes.ObtenerMisSolicitudesAsync(UsuarioId);

        // Validación server-side: si los filtros son inválidos no se aplican y se muestran los errores.
        var visibles = ModelState.IsValid ? filtro.Aplicar(todas).ToList() : todas.ToList();

        return View(new MisSolicitudesViewModel
        {
            Filtro = filtro,
            Solicitudes = visibles,
            TotalSinFiltrar = todas.Count,
            TienePerfilCliente = cliente is not null
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

        return View(solicitud);
    }
}
