# Plataforma de Créditos

Plataforma web interna para gestionar solicitudes de crédito — Examen Parcial 2026-1.

**URL en Render:** _pendiente de publicar_

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

| Variable | Valor en Render | Descripción |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Entorno |
| `ASPNETCORE_URLS` | `http://0.0.0.0:${PORT}` | Referencia; el **comando de inicio** la sobrescribe con el `PORT` real (ver abajo) |
| `ConnectionStrings__DefaultConnection` | `Data Source=/var/data/plataforma-creditos.db` | Archivo SQLite |
| `Redis__ConnectionString` | `redis://default:<clave>@<host>:<puerto>` | Redis Cloud (sesión, caché, Data Protection) |
| `RabbitMq__ConnectionString` | `amqps://<usuario>:<clave>@<host>/<vhost>` | URI AMQPS de CloudAMQP |
| `RabbitMq__QueueName` | `solicitudes.notificaciones` | Cola durable |
| `RabbitMq__ConsumerEnabled` | `true` | Activa el consumidor (`BackgroundService`) |

> Nunca se suben credenciales al repositorio: en local se usan `appsettings.Development.json` (solo `localhost`) o *user-secrets*; en Render, variables de entorno.

## Despliegue en Render (Pregunta 8)

Archivos: [`Dockerfile`](Dockerfile), [`render.yaml`](render.yaml) (Blueprint) y [`.dockerignore`](.dockerignore).

### Pasos

1. En Render: **New → Blueprint** → elegir este repositorio → Render lee `render.yaml`.
   (Alternativa manual: **New → Web Service** → repositorio → *Language: Docker*, *Instance type: Free*, rama `main`).
2. Completar las variables marcadas como secretas (`Redis__ConnectionString`, `RabbitMq__ConnectionString`). Las demás ya vienen en el Blueprint.
3. **Deploy**. Cada merge a `main` redespliega automáticamente (`autoDeploy: true`).
4. Verificar `https://<servicio>.onrender.com/healthz` → `Healthy`.

### Comando de inicio y PORT

Render inyecta `PORT` en tiempo de ejecución. `${PORT}` **no** se expande dentro del valor de otra variable de entorno, por eso se expande en el comando de inicio (Dockerfile `CMD` y `dockerCommand` del Blueprint):

```sh
sh -c 'export ASPNETCORE_URLS="http://0.0.0.0:${PORT}" && exec dotnet PlataformaCreditos.dll'
```

### HTTPS / WSS detrás del proxy

Render termina TLS en su proxy. `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (en el Dockerfile) hace que la app respete `X-Forwarded-Proto`, de modo que las cookies seguras, las redirecciones y el WebSocket (`wss://`) funcionen correctamente.

### Persistencia de SQLite

- Las migraciones se aplican y los datos iniciales se siembran **al arrancar** (idempotente).
- **Plan Free (configuración actual):** el sistema de archivos es efímero, así que en cada despliegue o reinicio SQLite se recrea y se vuelve a sembrar con los usuarios demo. Las sesiones y el login **no** se pierden porque las llaves de Data Protection están en Redis.
- **Para conservar SQLite** entre despliegues y reinicios: plan `starter` + disco persistente montado en `/var/data` (bloque `disk` comentado en `render.yaml`). Como `ConnectionStrings__DefaultConnection` apunta a `/var/data/plataforma-creditos.db`, el archivo queda en el disco y sobrevive a despliegues y reinicios.
- Se ejecuta **una sola instancia** (`numInstances: 1`): SQLite es un archivo local y el consumidor de RabbitMQ corre como `BackgroundService` dentro del mismo proceso.
- En el plan Free el servicio se suspende tras ~15 min sin tráfico; al despertar, el consumidor procesa los mensajes que quedaron en la cola durable.

### Probar la imagen en local

```bash
docker build -t plataforma-creditos .
docker run -p 8080:10000 -e PORT=10000 -e Redis__ConnectionString=host.docker.internal:6379 plataforma-creditos
```

## Flujo de trabajo Git

Cada pregunta se desarrolla en su propia rama creada desde `main` actualizado y se integra mediante Pull Request.

| Pregunta | Rama |
|---|---|
| 1. Bootstrap + modelo de datos | `feature/bootstrap-dominio` |
| 2. Catálogo de solicitudes y filtros | `feature/catalogo-solicitudes` |
| 3. Registro y validaciones de solicitud | `feature/solicitudes` |
| 4. Sesiones y Redis | `feature/sesion-redis` |
| 8. Despliegue en Render | `deploy/render` |
