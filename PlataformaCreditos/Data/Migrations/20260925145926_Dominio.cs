using System;
using Microsoft.EntityFrameworkCore.Migrations;
using PlataformaCreditos.Data;

#nullable disable

namespace PlataformaCreditos.Data.Migrations
{
    /// <inheritdoc />
    public partial class Dominio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: false),
                    IngresosMensuales = table.Column<double>(type: "REAL", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                    table.CheckConstraint("CK_Clientes_IngresosMensuales_Positivos", "\"IngresosMensuales\" > 0");
                    table.ForeignKey(
                        name: "FK_Clientes_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudesCredito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    MontoSolicitado = table.Column<double>(type: "REAL", nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MotivoRechazo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesCredito", x => x.Id);
                    table.CheckConstraint("CK_SolicitudesCredito_Estado_Valido", "\"Estado\" IN ('Pendiente', 'Aprobado', 'Rechazado')");
                    table.CheckConstraint("CK_SolicitudesCredito_Monto_Positivo", "\"MontoSolicitado\" > 0");
                    table.CheckConstraint("CK_SolicitudesCredito_Rechazo_ConMotivo", "\"Estado\" <> 'Rechazado' OR (\"MotivoRechazo\" IS NOT NULL AND length(trim(\"MotivoRechazo\")) > 0)");
                    table.ForeignKey(
                        name: "FK_SolicitudesCredito_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_UsuarioId",
                table: "Clientes",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesCredito_ClienteId_FechaSolicitud",
                table: "SolicitudesCredito",
                columns: new[] { "ClienteId", "FechaSolicitud" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesCredito_UnaPendientePorCliente",
                table: "SolicitudesCredito",
                column: "ClienteId",
                unique: true,
                filter: "\"Estado\" = 'Pendiente'");

            // Regla: no se puede aprobar una solicitud cuyo monto supere 5 veces los ingresos mensuales.
            // CHECK no puede consultar otra tabla, por eso se implementa con triggers.
            foreach (var (nombre, evento) in new[]
                     {
                         (ApplicationDbContext.TriggerAprobacionInsert, "INSERT"),
                         (ApplicationDbContext.TriggerAprobacionUpdate, "UPDATE OF \"Estado\", \"MontoSolicitado\"")
                     })
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER "{nombre}"
                    BEFORE {evento} ON "SolicitudesCredito"
                    FOR EACH ROW
                    WHEN NEW."Estado" = 'Aprobado'
                     AND NEW."MontoSolicitado" > 5 * (SELECT "IngresosMensuales" FROM "Clientes" WHERE "Id" = NEW."ClienteId")
                    BEGIN
                        SELECT RAISE(ABORT, 'No se puede aprobar: el monto supera 5 veces los ingresos mensuales');
                    END;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS \"{ApplicationDbContext.TriggerAprobacionInsert}\";");
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS \"{ApplicationDbContext.TriggerAprobacionUpdate}\";");

            migrationBuilder.DropTable(
                name: "SolicitudesCredito");

            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
