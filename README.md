# Plataforma de Créditos

Plataforma web interna para gestionar solicitudes de crédito — Examen Parcial 2026-1.

**Stack:** ASP.NET Core MVC (.NET 10) + Identity · EF Core + SQLite · Razor Views · Redis (sesión y caché)

## Requisitos locales

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Herramienta EF Core: `dotnet tool install --global dotnet-ef`
- Redis local (opcional, recomendado): `docker run -d --name pc-redis -p 6379:6379 redis:7-alpine`

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

## Funcionalidades

### Mis solicitudes (Pregunta 2)

- `GET /Solicitudes` — listado de las solicitudes del usuario autenticado (`[Authorize]`).
- Filtros: **Estado**, **rango de monto** (mín./máx.) y **rango de fechas** (desde/hasta, hora de Lima, inclusivos).
- `GET /Solicitudes/Detalle/{id}` — detalle completo (monto, fecha, estado, motivo, ingresos, relación monto/ingresos). Si la solicitud es de otro usuario responde **404**.
- Validaciones **server-side** (`FiltroSolicitudesViewModel : IValidatableObject`):
  - montos negativos → error; mínimo > máximo → error;
  - fecha inicio > fecha fin → error.
  - Si los filtros son inválidos no se aplican y se muestran los mensajes en la misma vista.

### Registro de solicitudes (Pregunta 3)

- `GET/POST /Solicitudes/Registrar` — formulario que crea una `SolicitudCredito` en estado **Pendiente**.
- Validaciones **en el servidor** (`SolicitudesService.RegistrarAsync`), con el usuario tomado de la sesión autenticada (nunca del formulario):
  1. Usuario autenticado (`[Authorize]` + `[ValidateAntiForgeryToken]`).
  2. El cliente existe y está **activo**.
  3. No existe otra solicitud **Pendiente** del cliente (además, índice único filtrado en BD para envíos simultáneos).
  4. `MontoSolicitado > 0` y `MontoSolicitado ≤ 10 × IngresosMensuales`.
- Feedback de éxito o error **en la misma vista** (el formulario se limpia tras un registro exitoso).
- `GET/POST /Solicitudes/Perfil` — un usuario recién registrado declara sus ingresos para crear su perfil de cliente.

Casos de prueba rápidos (contraseña `Demo123!`):

| Usuario | Acción | Resultado esperado |
|---|---|---|
| `cliente1` | Registrar cualquier monto | Error: ya tiene una solicitud Pendiente |
| `cliente3` | Registrar cualquier monto | Error: cliente inactivo |
| `cliente2` | Registrar 50 001 | Error: supera 10 × 5 000 |
| `cliente2` | Registrar 50 000 | Éxito, queda Pendiente |

### Sesión y caché con Redis (Pregunta 4)

| Uso | Implementación |
|---|---|
| **Sesión** (Redis-backed) | `AddSession()` sobre `IDistributedCache` de Redis. Al abrir un detalle se guarda la última solicitud visitada (`UltimaSolicitudSesion`) y el layout muestra **"Ver última solicitud S/ {Monto}"** (`UltimaSolicitudViewComponent`). El valor se asocia al usuario para que otro usuario en el mismo navegador no lo vea. |
| **Caché** del listado | `SolicitudesCache`: clave `PlataformaCreditos:solicitudes:usuario:{UsuarioId}`, expiración absoluta **60 s**. Los filtros se aplican sobre la lista cacheada. La vista muestra la fuente: *caché (60 s)* o *base de datos*. |
| **Invalidación** | Al **registrar** una solicitud (`SolicitudesService.RegistrarAsync`) y al **cambiar su estado** (panel Analista). |
| **Data Protection** | Las llaves se guardan en Redis (`PlataformaCreditos:DataProtection-Keys`) para que las cookies de login y sesión sigan siendo válidas tras un reinicio o redespliegue. |

- `Redis:ConnectionString` acepta el formato de StackExchange (`host:puerto,password=...,ssl=True`) o el URI de Redis Cloud (`redis://default:<clave>@host:puerto`, `rediss://` para TLS).
- En **Development** sin Redis configurado se usa caché en memoria; en **Production** la variable es obligatoria.
- Si Redis falla temporalmente, se registra el error y la consulta se resuelve contra la base de datos.

Comprobar en Redis: `docker exec pc-redis redis-cli --scan` → `PlataformaCreditos:solicitudes:usuario:<id>`, `PlataformaCreditos:<id-sesion>`, `PlataformaCreditos:DataProtection-Keys`.

## Variables de entorno

| Variable | Ejemplo | Descripción |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | `Data Source=app.db` | Ruta del archivo SQLite |
| `Redis__ConnectionString` | `redis://default:<clave>@<host>:<puerto>` | Redis Cloud (sesión, caché, Data Protection) |

> Nunca se suben credenciales al repositorio: en local se usan `appsettings.Development.json` (solo `localhost`) o *user-secrets*; en Render, variables de entorno.

## Flujo de trabajo Git

Cada pregunta se desarrolla en su propia rama creada desde `main` actualizado y se integra mediante Pull Request.

| Pregunta | Rama |
|---|---|
| 1. Bootstrap + modelo de datos | `feature/bootstrap-dominio` |
| 2. Catálogo de solicitudes y filtros | `feature/catalogo-solicitudes` |
| 3. Registro y validaciones de solicitud | `feature/solicitudes` |
| 4. Sesiones y Redis | `feature/sesion-redis` |
