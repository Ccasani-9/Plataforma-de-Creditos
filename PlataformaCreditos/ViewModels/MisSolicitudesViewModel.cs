namespace PlataformaCreditos.ViewModels;

public class MisSolicitudesViewModel
{
    public FiltroSolicitudesViewModel Filtro { get; set; } = new();

    public IReadOnlyList<SolicitudResumen> Solicitudes { get; set; } = [];

    public int TotalSinFiltrar { get; set; }

    public bool TienePerfilCliente { get; set; }

    public bool DesdeCache { get; set; }
}
