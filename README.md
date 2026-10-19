# Ordali

Sistema de gestión de almacén (SGA) con ASP.NET Core, EF Core y SQL Server, pensado para funcionar igual en un servidor local que en la nube.

> Estado: en desarrollo. Esta versión contiene el esqueleto del proyecto: API, health checks, Swagger, Docker y CI.

## Probarlo en un minuto

Solo necesitas [Docker](https://www.docker.com/products/docker-desktop/).

```bash
git clone https://github.com/Yametekds/Ordali.git
cd Ordali
docker compose up --build
```

Después abre http://localhost:8080/swagger.

## Desarrollo local

Requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/Ordali.Api
dotnet test
```

## Endpoints disponibles

| Ruta | Para qué sirve |
| --- | --- |
| `/swagger` | Documentación interactiva de la API |
| `/health/live` | El proceso está en marcha |
| `/health/ready` | La API puede atender peticiones (incluye la base de datos) |

## Hoja de ruta

1. **Núcleo:** catálogo, ubicaciones, stock y movimientos, JWT y perfiles con permisos.
2. **Interfaces:** web Blazor (PWA) y app .NET MAUI.
3. **Operación:** entradas, salidas, órdenes y tareas de almacén, webhooks.
4. **Sector:** GS1 (GTIN, SSCC), EDI y asistente con permisos por rol.
