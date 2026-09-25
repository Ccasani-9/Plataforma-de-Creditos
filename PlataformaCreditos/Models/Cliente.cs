using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models;

public class Cliente
{
    public int Id { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [Display(Name = "Ingresos mensuales")]
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "Los ingresos mensuales deben ser mayores a 0.")]
    [DataType(DataType.Currency)]
    public decimal IngresosMensuales { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<SolicitudCredito> Solicitudes { get; set; } = new List<SolicitudCredito>();

    public decimal MontoMaximoAprobable => IngresosMensuales * ReglasCredito.MultiploMaximoAprobacion;

    public decimal MontoMaximoRegistrable => IngresosMensuales * ReglasCredito.MultiploMaximoRegistro;
}
