# Plataforma de Créditos

Plataforma web interna para gestionar solicitudes de crédito — Examen Parcial 2026-1.

**Stack:** ASP.NET Core MVC (.NET 10) + Identity · EF Core + SQLite · Razor Views

## Requisitos locales

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Herramienta EF Core: `dotnet tool install --global dotnet-ef`

## Ejecutar en local

```bash
git clone https://github.com/Ccasani-9/Plataforma-de-Creditos.git
cd Plataforma-de-Creditos
dotnet run --project PlataformaCreditos --launch-profile http
# http://localhost:5091
```

Al iniciar, la aplicación **aplica las migraciones automáticamente** y carga los datos iniciales (idempotente).

## Migraciones

```bash
# Crear una migración nueva
dotnet ef migrations add <Nombre> --project PlataformaCreditos -o Data/Migrations
# Aplicar migraciones manualmente (opcional: también se aplican al iniciar)
dotnet ef database update --project PlataformaCreditos
```

| Migración | Contenido |
|---|---|
| `CreateIdentitySchema` | Tablas de ASP.NET Core Identity |
| `Dominio` | `Clientes`, `SolicitudesCredito`, CHECK constraints, índice único filtrado y triggers |

## Modelo de datos y restricciones

- **Cliente**: `Id`, `UsuarioId` (FK única a `AspNetUsers`), `IngresosMensuales`, `Activo`.
- **SolicitudCredito**: `Id`, `ClienteId`, `MontoSolicitado`, `FechaSolicitud`, `Estado {Pendiente, Aprobado, Rechazado}`, `MotivoRechazo`.

| Regla | Dónde se garantiza |
|---|---|
| `IngresosMensuales > 0` | `CHECK CK_Clientes_IngresosMensuales_Positivos` + `[Range]` |
| `MontoSolicitado > 0` | `CHECK CK_SolicitudesCredito_Monto_Positivo` + `[Range]` |
| Una sola solicitud `Pendiente` por cliente | Índice único filtrado `IX_SolicitudesCredito_UnaPendientePorCliente` (`WHERE Estado = 'Pendiente'`) |
| No aprobar si `MontoSolicitado > 5 × IngresosMensuales` | `SolicitudCredito.Aprobar()` (dominio) + triggers SQLite `TR_SolicitudesCredito_Aprobacion_*` |
| Rechazo con motivo | `SolicitudCredito.Rechazar()` + `CHECK CK_SolicitudesCredito_Rechazo_ConMotivo` |

> SQLite no tiene tipo decimal: los montos se guardan como `REAL` para que los filtros por rango y los `CHECK` se evalúen en la base de datos.

## Datos iniciales

Contraseña de todos los usuarios demo: **`Demo123!`**

| Usuario | Rol / Cliente | Ingresos | Solicitudes |
|---|---|---|---|
| `analista@creditos.pe` | Rol **Analista** | — | — |
| `cliente1@creditos.pe` | Cliente activo | 3 000 | 12 000 **Pendiente** |
| `cliente2@creditos.pe` | Cliente activo | 5 000 | 20 000 **Aprobado** |
| `cliente3@creditos.pe` | Cliente **inactivo** | 2 500 | — |

## Flujo de trabajo Git

Cada pregunta se desarrolla en su propia rama creada desde `main` actualizado y se integra mediante Pull Request.

| Pregunta | Rama |
|---|---|
| 1. Bootstrap + modelo de datos | `feature/bootstrap-dominio` |
