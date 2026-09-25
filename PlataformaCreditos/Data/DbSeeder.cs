using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

/// <summary>
/// Aplica migraciones y carga la configuración inicial (idempotente):
/// rol Analista + usuario analista, 3 clientes (2 activos, 1 inactivo) y
/// 2 solicitudes (una Pendiente y una Aprobada).
/// </summary>
public static class DbSeeder
{
    public const string PasswordDemo = "Demo123!";

    public const string EmailAnalista = "analista@creditos.pe";
    public const string EmailCliente1 = "cliente1@creditos.pe";
    public const string EmailCliente2 = "cliente2@creditos.pe";
    public const string EmailClienteInactivo = "cliente3@creditos.pe";

    public static async Task InicializarAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbSeeder));
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();

        AsegurarDirectorioSqlite(db.Database.GetConnectionString(), logger);
        await db.Database.MigrateAsync();

        if (!await roleManager.RoleExistsAsync(Roles.Analista))
        {
            await roleManager.CreateAsync(new IdentityRole(Roles.Analista));
        }

        var analista = await AsegurarUsuarioAsync(userManager, EmailAnalista);
        if (!await userManager.IsInRoleAsync(analista, Roles.Analista))
        {
            await userManager.AddToRoleAsync(analista, Roles.Analista);
        }

        var usuario1 = await AsegurarUsuarioAsync(userManager, EmailCliente1);
        var usuario2 = await AsegurarUsuarioAsync(userManager, EmailCliente2);
        var usuario3 = await AsegurarUsuarioAsync(userManager, EmailClienteInactivo);

        if (await db.Clientes.AnyAsync())
        {
            return;
        }

        var cliente1 = new Cliente { UsuarioId = usuario1.Id, IngresosMensuales = 3000m, Activo = true };
        var cliente2 = new Cliente { UsuarioId = usuario2.Id, IngresosMensuales = 5000m, Activo = true };
        var cliente3 = new Cliente { UsuarioId = usuario3.Id, IngresosMensuales = 2500m, Activo = false };
        db.Clientes.AddRange(cliente1, cliente2, cliente3);

        var ahora = DateTime.UtcNow;
        db.SolicitudesCredito.AddRange(
            new SolicitudCredito
            {
                Cliente = cliente1,
                MontoSolicitado = 12000m,
                FechaSolicitud = ahora.AddDays(-2),
                Estado = EstadoSolicitud.Pendiente
            },
            new SolicitudCredito
            {
                Cliente = cliente2,
                MontoSolicitado = 20000m,
                FechaSolicitud = ahora.AddDays(-10),
                Estado = EstadoSolicitud.Aprobado
            });

        await db.SaveChangesAsync();
        logger.LogInformation("Datos iniciales cargados: 3 clientes, 2 solicitudes y usuario Analista.");
    }

    /// <summary>Crea la carpeta del archivo SQLite (p. ej. el disco persistente /var/data en Render).</summary>
    private static void AsegurarDirectorioSqlite(string? connectionString, ILogger logger)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        var directorio = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
        {
            Directory.CreateDirectory(directorio);
        }
        logger.LogInformation("Base de datos SQLite: {Ruta}", Path.GetFullPath(dataSource));
    }

    private static async Task<IdentityUser> AsegurarUsuarioAsync(UserManager<IdentityUser> userManager, string email)
    {
        var usuario = await userManager.FindByEmailAsync(email);
        if (usuario is not null)
        {
            return usuario;
        }

        usuario = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var resultado = await userManager.CreateAsync(usuario, PasswordDemo);
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo crear el usuario {email}: {string.Join("; ", resultado.Errors.Select(e => e.Description))}");
        }

        return usuario;
    }
}
