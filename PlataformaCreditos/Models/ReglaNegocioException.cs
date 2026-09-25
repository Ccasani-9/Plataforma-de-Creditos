namespace PlataformaCreditos.Models;

/// <summary>Violación de una regla de negocio; su mensaje es apto para mostrarse al usuario.</summary>
public class ReglaNegocioException(string message) : Exception(message);
