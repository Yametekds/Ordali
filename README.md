# Ordali

Sistema de gestión de almacén (SGA): una API con ASP.NET Core, EF Core y SQL Server, pensada para funcionar igual en un servidor local que en la nube, y una web y una app que la usan.

> Estado: en desarrollo. Ya funcionan el login con JWT, los perfiles con permisos y plantillas, el catálogo de artículos, las ubicaciones, el stock, las entradas y el registro de movimientos. La web Blazor (PWA) y la app .NET MAUI para Android, Windows y macOS usan la misma API. Las instrucciones de la app están en [src/Ordali.App/README.md](src/Ordali.App/README.md).

## Probarlo en un minuto

Solo necesitas [Docker](https://www.docker.com/products/docker-desktop/).

```bash
git clone https://github.com/Yametekds/Ordali.git
cd Ordali
docker compose up --build
```

Después abre:

- http://localhost:8081: la web (usuarios demo `admin@demo.local`, `encargado@demo.local` y `mozo@demo.local`, contraseña `Demo1234!`).
- http://localhost:8080/swagger: la documentación interactiva de la API.

### Demo online

La web en modo demo (datos de ejemplo en el navegador, sin servidor) se publica en GitHub Pages: `https://yametekds.github.io/Ordali/`. Más detalles en [src/Ordali.Web/README.md](src/Ordali.Web/README.md).

### Sin instalar nada

Pulsa **Code → Codespaces → Create codespace** en GitHub. El entorno arranca la API, la web y SQL Server solo, y abre la web en el navegador; la API queda en el puerto 8080. Usa la cuota gratuita mensual de tu cuenta de GitHub.

## Desarrollo local

Requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download) y Docker.

```bash
dotnet run --project src/Ordali.Api
dotnet test
```

Sin cadena de conexión, la API usa una base de datos en memoria con los datos de demo, así que no hace falta nada más. Para trabajar contra SQL Server:

```bash
docker compose up -d sqlserver
ConnectionStrings__Ordali="Server=localhost;Database=Ordali;User Id=sa;Password=Demo_Passw0rd!;TrustServerCertificate=True" \
  dotnet run --project src/Ordali.Api
```

Las migraciones se aplican al arrancar (`Database:MigrateOnStartup`). En producción se puede desactivar y aplicarlas como un paso aparte con `dotnet ef database update`.

## Endpoints disponibles

| Ruta | Para qué sirve |
| --- | --- |
| `/swagger` | Documentación interactiva de la API (botón *Authorize* con el token del login) |
| `POST /api/v1/auth/login`, `GET /api/v1/auth/me` | Token JWT y el usuario actual con sus permisos |
| `GET /api/v1/products?search=` | Artículos; busca por SKU, nombre o código de barras |
| `POST`/`PUT /api/v1/products` | Alta y edición de artículos |
| `GET /api/v1/sites`, `GET`/`POST /api/v1/locations` | Almacenes y ubicaciones; un código repetido pide confirmación (409 y `allowDuplicate`) |
| `GET /api/v1/stock` | Stock por artículo, ubicación y lote |
| `GET`/`POST /api/v1/receipts` | Entradas: la mercancía queda en recepción hasta que se ubica; `?pending=true` lista las pendientes |
| `POST /api/v1/receipts/{id}/putaway` | Traslada parte de una entrada desde recepción a su ubicación |
| `GET`/`POST /api/v1/movements` | Movimientos (salidas, traslados y ajustes) con quién los hizo y la entrada a la que pertenecen |
| `GET /api/v1/permissions` | Los permisos que se pueden marcar en una plantilla |
| `/api/v1/profile-templates` | Plantillas de perfil: las crea el administrador y las usan quienes pueden crear perfiles |
| `GET`/`POST /api/v1/users` | Perfiles por categoría y alta a partir de una plantilla |
| `PUT /api/v1/users/{id}/access` | Cambiar categoría, permisos especiales o desactivar un perfil (solo administrador) |
| `/health/live` | El proceso está en marcha |
| `/health/ready` | La API puede atender peticiones (incluye la base de datos) |

## Entradas, traslados y lectores de códigos de barras

1. **Entrada:** el mozo registra lo que llega con un código (por ejemplo `ENTRADA2`, o `ENT-0003` si lo deja vacío). La mercancía queda en la ubicación de recepción.
2. **Traslado:** otro mozo escanea `ENTRADA2`, escanea el artículo y le aparece su ubicación predeterminada; si no tiene, escanea o elige otra. Escribe la cantidad y la entrada se cierra cuando no queda nada en recepción.

Las pantallas funcionan con lectores USB, Bluetooth y terminales Android (Zebra, Honeywell…) que escriben el código y pulsan Intro: cada paso deja el cursor en el campo siguiente y avisa con un pitido si el código no encaja. En móviles con Chrome o Edge también se puede escanear con la cámara.

## Perfiles y permisos

Cada perfil tiene una **categoría** (Mozo, Encargado o Administrador) y una lista de **permisos** que se marcan uno a uno: ver artículos, editarlos, ver stock, registrar movimientos, registrar ajustes, ver usuarios, crear perfiles...

- El **administrador** tiene todos los permisos. Además, es el único que crea **plantillas de perfil** y que puede cambiar un perfil ya creado: darle permisos especiales, subirlo de categoría o desactivarlo.
- Las plantillas se comparten con todos los que tienen el permiso *crear perfiles*, que las usan para dar de alta perfiles nuevos. Nadie salvo el administrador puede usar una plantilla con permisos que él mismo no tiene.
- Los permisos se comprueban en la API en cada petición, así que un cambio se aplica al momento.

Perfiles de ejemplo (contraseña `Demo1234!`):

| Usuario | Categoría | Plantilla |
| --- | --- | --- |
| `admin@demo.local` | Administrador | Todos los permisos |
| `encargado@demo.local` | Encargado | Encargado de turno: todos los permisos que se pueden dar |
| `mozo@demo.local` | Mozo | Mozo de almacén: consultar y registrar movimientos, sin ajustes |

Cada movimiento guarda el usuario y la hora, y no se puede modificar ni borrar: los errores se corrigen con un ajuste.

## Estructura

```
src/Ordali.Api                    API (módulos de identidad, catálogo, ubicaciones e inventario)
src/Ordali.Client                 Cliente de la API para las interfaces
src/Ordali.Ui                     Componentes Razor compartidos por las interfaces
src/Ordali.Web                    Web Blazor WebAssembly (PWA)
src/Ordali.App                    App .NET MAUI para Android, Windows y macOS
tests/Ordali.Api.Tests            Tests de integración de la API con WebApplicationFactory
tests/Ordali.Ui.Tests             Tests de los componentes de la interfaz
.github/workflows/ci.yml          Build, tests y prueba del stack con Docker Compose
.github/workflows/pages.yml       Publica la demo web en GitHub Pages
.github/workflows/apps.yml        Compila la app y, con una etiqueta v*, la publica en Releases
.devcontainer/                    Configuración de GitHub Codespaces
```

## Hoja de ruta

1. **Operación:** salidas, órdenes y tareas de almacén, webhooks.
2. **Sector:** GS1 (GTIN, SSCC), EDI y asistente con permisos por rol.
3. **Escala:** caché distribuida, colas y réplica de lectura.
