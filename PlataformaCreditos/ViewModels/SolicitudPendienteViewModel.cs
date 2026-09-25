using PlataformaCreditos.Models;

namespace PlataformaCreditos.ViewModels;

public record SolicitudPendienteViewModel(
    int Id,
    string EmailCliente,
    decimal IngresosMensuales,
    bool ClienteActivo,
    decimal MontoSolicitado,
    DateTime FechaSolicitud)
{
    public decimal MontoMaximoAprobable => IngresosMensuales * ReglasCredito.MultiploMaximoAprobacion;

    public bool ExcedeLimiteAprobacion => MontoSolicitado > MontoMaximoAprobable;

    public decimal Relacion => IngresosMensuales == 0 ? 0 : MontoSolicitado / IngresosMensuales;
}
