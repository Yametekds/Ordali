# Ordali Web (Blazor WebAssembly PWA)

Interfaz web de Ordali. Se instala como app en el móvil y funciona sin conexión una vez cargada. Cuando se abre desde el navegador de un móvil, muestra cómo instalarla: con un botón en Android y con los pasos de Safari en iPhone.

## Proyectos

| Proyecto | Qué contiene |
| --- | --- |
| `Ordali.Client` | Cliente de la API: modelos, `IOrdaliApi`, sesión y un backend en memoria para el modo demo. No depende de Blazor. |
| `Ordali.Ui` | Librería de componentes Razor con todas las pantallas, el layout y los estilos. La usan la web y la app MAUI. |
| `Ordali.Web` | Host Blazor WebAssembly: arranque, PWA (manifest y service worker) y configuración. |

## Modo demo y modo API

`wwwroot/appsettings.json` decide contra qué trabaja la web:

```json
{ "Api": { "BaseUrl": "" } }
```

- **Vacío:** modo demo. Los datos de ejemplo viven en el navegador y se pierden al recargar. Es lo que se publica en GitHub Pages, para probarlo sin instalar nada.
- **Con URL** (por ejemplo `http://localhost:8080`): usa la API real con JWT.

Usuarios del modo demo (contraseña `Demo1234!`):

| Usuario | Categoría | Puede |
| --- | --- | --- |
| `admin@demo.local` | Administrador | Todo: crear plantillas de perfil y cambiar permisos o categoría de cualquier perfil. |
| `encargado@demo.local` | Encargado | Plantilla "Encargado de turno": catálogo, ajustes de inventario y alta de perfiles desde plantillas. |
| `mozo@demo.local` | Mozo | Plantilla "Mozo de almacén": consultar y registrar entradas, salidas y traslados. |

## Perfiles y permisos

- Cada perfil tiene una **categoría** (Mozo, Encargado o Administrador) y una lista de **permisos**.
- Las **plantillas de perfil** son conjuntos de permisos que se marcan con casillas. Solo el administrador las crea y edita, pero cualquiera con permiso para crear perfiles puede usarlas, siempre que no den permisos que él no tenga.
- Solo el administrador cambia los permisos de un perfil ya creado (puede dar permisos especiales fuera de su plantilla), lo sube de categoría o lo desactiva.
- En el menú lateral, **Registros** agrupa Productos y Usuarios, con un acceso directo por categoría.

## Ejecutar en local

```bash
dotnet run --project src/Ordali.Web
```

## Docker

La imagen sirve la web con nginx. La variable `API_BASE_URL` apunta a la API; sin ella arranca en modo demo.

```bash
docker build -f src/Ordali.Web/Dockerfile -t ordali-web .
docker run -p 8081:80 -e API_BASE_URL=http://localhost:8080 ordali-web
```

## GitHub Pages

El workflow `.github/workflows/pages.yml` publica la web en modo demo en cada push a `main`. Hay que activarlo una vez en **Settings → Pages → Source: GitHub Actions**.

## Contrato con la API

Todas las rutas bajo `/api/v1`, JSON en camelCase y enums como texto (`"Transfer"`). Los errores llegan como ProblemDetails; los de validación, con `errors` por campo. Los permisos se comprueban en cada petición.

| Método | Ruta | Permiso |
| --- | --- | --- |
| POST | `/auth/login` → `{ accessToken, expiresAt, user: { id, name, email, category, permissions } }` | — |
| GET | `/auth/me` | sesión iniciada |
| GET | `/products?search=`, `/products/{id}` | `products.view` |
| POST / PUT | `/products`, `/products/{id}` | `products.edit` |
| GET | `/sites`, `/locations?siteId=` | `locations.view` |
| POST | `/locations` (`{ code, zone, type, allowDuplicate }`; 409 si el código ya existe) | `locations.edit` |
| GET | `/stock?productId=&locationId=` | `stock.view` |
| GET | `/movements?take=` | `movements.view` |
| POST | `/movements` (salidas, traslados y ajustes; las entradas van por `/receipts`) | `movements.create` (y `movements.adjust` para ajustes) |
| GET | `/receipts?pending=&code=`, `/receipts/{id}` | `movements.view` |
| POST | `/receipts` (`{ code, reference, lines: [{ productId, lot, quantity }] }`) | `movements.create` |
| POST | `/receipts/{id}/putaway` (`{ productId, lot, toLocationId, quantity }`) | `movements.create` |
| GET | `/profile-templates`, `/profile-templates/{id}` | `users.view` o `users.create` |
| POST / PUT / DELETE | `/profile-templates`, `/profile-templates/{id}` | Administrador |
| GET | `/users`, `/users/{id}` | `users.view` |
| POST | `/users` (`{ name, email, password, templateId }`) | `users.create` |
| PUT | `/users/{id}/access` (`{ category, permissions, active }`) | Administrador |

Los modelos exactos están en `src/Ordali.Client/Models`.

## Logo y fuentes

- `src/Ordali.Ui/wwwroot/img/ordali-logo.png`: logo sin fondo. `ordali-icon.png`: logo sobre fondo oscuro, para iconos de app.
- Roboto y Roboto Condensed van incluidas en `src/Ordali.Ui/wwwroot/fonts` (licencia SIL OFL), para que la app funcione sin conexión.
