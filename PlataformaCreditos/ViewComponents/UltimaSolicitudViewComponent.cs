using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PlataformaCreditos.Infrastructure;

namespace PlataformaCreditos.ViewComponents;

/// <summary>Enlace "Ver última solicitud {Monto}" del layout, leído desde la sesión.</summary>
public class UltimaSolicitudViewComponent : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Content(string.Empty);
        }

        await HttpContext.Session.LoadAsync();
        var usuarioId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var ultima = UltimaSolicitudSesion.Obtener(HttpContext.Session, usuarioId);
        return ultima is null ? Content(string.Empty) : View(ultima);
    }
}
