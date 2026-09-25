using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public const string TriggerAprobacionInsert = "TR_SolicitudesCredito_Aprobacion_Insert";
    public const string TriggerAprobacionUpdate = "TR_SolicitudesCredito_Aprobacion_Update";

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<SolicitudCredito> SolicitudesCredito => Set<SolicitudCredito>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Cliente>(entity =>
        {
            entity.ToTable("Clientes", t =>
                t.HasCheckConstraint("CK_Clientes_IngresosMensuales_Positivos", "\"IngresosMensuales\" > 0"));

            // SQLite no tiene tipo decimal nativo: se guarda como REAL para permitir
            // comparaciones, filtros por rango y CHECK constraints en la base de datos.
            entity.Property(c => c.IngresosMensuales).HasConversion<double>();
            entity.Property(c => c.Activo).HasDefaultValue(true);

            entity.HasIndex(c => c.UsuarioId).IsUnique();
            entity.HasOne<IdentityUser>()
                .WithOne()
                .HasForeignKey<Cliente>(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SolicitudCredito>(entity =>
        {
            entity.ToTable("SolicitudesCredito", t =>
            {
                t.HasCheckConstraint("CK_SolicitudesCredito_Monto_Positivo", "\"MontoSolicitado\" > 0");
                t.HasCheckConstraint("CK_SolicitudesCredito_Estado_Valido",
                    "\"Estado\" IN ('Pendiente', 'Aprobado', 'Rechazado')");
                t.HasCheckConstraint("CK_SolicitudesCredito_Rechazo_ConMotivo",
                    "\"Estado\" <> 'Rechazado' OR (\"MotivoRechazo\" IS NOT NULL AND length(trim(\"MotivoRechazo\")) > 0)");

                // Triggers que impiden aprobar montos > 5x ingresos (creados en la migración).
                t.HasTrigger(TriggerAprobacionInsert);
                t.HasTrigger(TriggerAprobacionUpdate);
            });

            entity.Property(s => s.MontoSolicitado).HasConversion<double>();
            entity.Property(s => s.Estado)
                .HasConversion<string>()
                .HasMaxLength(20)
                // Concurrencia optimista: UPDATE ... WHERE Estado = <valor leído>. Evita que dos
                // analistas procesen la misma solicitud.
                .IsConcurrencyToken();
            entity.Property(s => s.MotivoRechazo).HasMaxLength(ReglasCredito.LongitudMaximaMotivo);

            // Un cliente solo puede tener UNA solicitud Pendiente (índice único filtrado).
            entity.HasIndex(s => s.ClienteId)
                .IsUnique()
                .HasFilter("\"Estado\" = 'Pendiente'")
                .HasDatabaseName("IX_SolicitudesCredito_UnaPendientePorCliente");

            entity.HasIndex(s => new { s.ClienteId, s.FechaSolicitud });

            entity.HasOne(s => s.Cliente)
                .WithMany(c => c.Solicitudes)
                .HasForeignKey(s => s.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
