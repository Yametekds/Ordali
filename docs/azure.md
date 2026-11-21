# Desplegar Ordali en Azure (gratis)

La API se publica en **Azure App Service**, la base de datos en **Azure SQL Database** y la web en **Azure Static Web Apps**. Los tres tienen un nivel gratuito permanente, así que el despliegue cuesta 0 € mientras no se cambie de plan. El workflow [`.github/workflows/azure.yml`](../.github/workflows/azure.yml) compila, prueba y publica las dos partes en cada push a `main`.

```
Navegador ──► Static Web Apps (web Blazor, PWA)
                    │  HTTPS + JWT
                    ▼
              App Service F1 (API ASP.NET Core) ──► Azure SQL Database (oferta gratuita)
```

## Qué se usa y qué límites tiene

| Pieza | Servicio y plan | Límites del plan gratuito | Coste |
| --- | --- | --- | --- |
| API | App Service, plan **Free F1** (Linux, .NET 10) | 60 min de CPU al día, 1 GB de RAM; se duerme tras unos 20 min sin peticiones | 0 € |
| Base de datos | Azure SQL Database, **oferta gratuita** (serverless) | 100 000 segundos de vCore y 32 GB al mes por base de datos; se pausa sola tras 1 h sin uso | 0 € |
| Web | Static Web Apps, plan **Free** | 100 GB de tráfico al mes, 250 MB por app | 0 € |

Con la opción *Pausar automáticamente la base de datos hasta el mes siguiente* la base de datos nunca factura: si se agotan los segundos de vCore, se para hasta el día 1. El plan F1 tampoco factura: si se agota la CPU del día, la API devuelve 403 hasta el día siguiente.

Lo que se nota en la demo: la primera petición después de un rato sin uso tarda entre 30 segundos y 1 minuto, porque se despiertan la API y la base de datos. Después va con normalidad.

## 1. Crear la cuenta de Azure

Hay dos caminos. Si eres estudiante, el primero no pide tarjeta.

### Opción A: Azure for Students (sin tarjeta)

1. Entra en <https://azure.microsoft.com/free/students> y pulsa **Activar ahora**.
2. Inicia sesión con una cuenta Microsoft (o crea una con tu correo personal).
3. Verifica que eres estudiante con el **correo de tu centro** (universidad, FP...). Llega un código o un enlace a ese correo.
4. Rellena nombre, país y teléfono y acepta. En unos minutos tienes la suscripción **Azure for Students** con 100 USD de crédito para 12 meses. Se renueva cada año mientras sigas siendo estudiante.

Si se acaba el crédito, la suscripción se desactiva en lugar de cobrarte. Todo lo de esta guía es gratuito, así que no gasta crédito.

### Opción B: cuenta gratuita de Azure (pide tarjeta)

1. Entra en <https://azure.microsoft.com/free> y pulsa **Empezar gratis** / **Pruébalo gratis**.
2. Inicia sesión con una cuenta Microsoft (o crea una).
3. Verifica tu **teléfono** con un SMS o una llamada.
4. Añade una **tarjeta**: solo sirve para comprobar la identidad. Puede aparecer una retención temporal de 1 € o 1 USD que se devuelve. No se cobra nada mientras no actualices la cuenta.
5. Acepta las condiciones. Tienes 200 USD de crédito durante 30 días y los servicios siempre gratuitos.

Al acabar los 30 días Azure pide **actualizar a pago por uso** para seguir usando la suscripción; si no lo haces, los recursos se paran. Al actualizar, los planes de esta guía siguen costando 0 €, porque son gratuitos para siempre, no parte del crédito. Para estar tranquilo, crea una alerta de gasto:

1. En el portal, busca **Cost Management** → **Presupuestos** → **Agregar**.
2. Ámbito: tu suscripción. Importe: 1 €. Alerta al 100 % con tu correo.

Así recibes un correo si algo empieza a costar dinero.

## 2. Crear un grupo de recursos

Todo va en un grupo de recursos, que luego se puede borrar de una vez.

1. En <https://portal.azure.com> busca **Grupos de recursos** → **Crear**.
2. Suscripción: la que acabas de crear. Nombre: `rg-ordali`. Región: **West Europe** (o la más cercana; usa la misma en todos los pasos).
3. **Revisar y crear** → **Crear**.

## 3. Base de datos: Azure SQL Database gratis

1. Busca **SQL databases** (Bases de datos SQL) → **Crear**.
2. Arriba aparece el aviso *¿Quiere probar Azure SQL Database gratis?*: pulsa **Aplicar oferta**. Si no aparece, es que la suscripción ya tiene sus bases de datos gratuitas usadas.
3. Grupo de recursos: `rg-ordali`. Nombre de la base de datos: `Ordali`.
4. Servidor: **Crear nuevo**.
   - Nombre: algo único, por ejemplo `ordali-sql-tunombre`.
   - Ubicación: la misma región.
   - Autenticación: **Usar autenticación de SQL**. Elige un usuario (por ejemplo `ordaliadmin`) y una contraseña larga. Apúntalos.
5. En *Comportamiento cuando se alcance el límite gratuito* elige **Pausar automáticamente la base de datos hasta el mes siguiente**.
6. Pestaña **Redes**: método de conectividad **Punto de conexión público** y **Permitir que los servicios y recursos de Azure accedan a este servidor: Sí**. Si quieres conectarte desde tu PC con SQL Server Management Studio o Azure Data Studio, activa también *Agregar la dirección IP del cliente actual*.
7. **Revisar y crear** → **Crear**. Tarda unos minutos.
8. Abre la base de datos → **Cadenas de conexión** → pestaña **ADO.NET (autenticación de SQL)** y copia la cadena. Sustituye `{your_password}` por tu contraseña y cambia `Connection Timeout=30` por `Connection Timeout=60`, para dar tiempo a que la base de datos se despierte.

No hace falta crear tablas: la API aplica las migraciones y carga los datos de ejemplo al arrancar.

## 4. API: App Service gratis

1. Busca **App Services** → **Crear** → **Aplicación web**.
2. Grupo de recursos: `rg-ordali`. Nombre: por ejemplo `ordali-api-tunombre`.
3. Si aparece **Nombre de host predeterminado único seguro**, puedes dejarlo activado o desactivarlo; solo cambia cómo queda la URL. Usa siempre la que aparece después en *Información general*.
4. Publicar: **Código**. Pila del entorno de ejecución: **.NET 10 (LTS)**. Sistema operativo: **Linux**. Región: la misma.
5. Plan de Linux: **Crear nuevo** (por ejemplo `plan-ordali`). Plan de precios: pulsa *Explorar planes de precios* y elige **Gratis F1**.
6. Pestaña **Implementación**: deja la implementación continua **desactivada** (el workflow del repositorio se encarga).
7. Pestaña **Supervisión**: **Application Insights: No**, para que no genere costes.
8. **Revisar y crear** → **Crear**.

Cuando termine, abre la aplicación web:

1. **Configuración** → **Variables de entorno** → **Configuración de la aplicación** y añade:

   | Nombre | Valor |
   | --- | --- |
   | `ConnectionStrings__Ordali` | La cadena del paso 3 |
   | `Jwt__SigningKey` | Una clave aleatoria de 32 caracteres o más (ver abajo) |
   | `Swagger__Enabled` | `true` |
   | `Database__SeedDemoData` | `true`: crea los perfiles de demo (`Demo1234!`). Quítalo si la API no es una demo pública |
   | `Cors__AllowedOrigins__0` | La URL de la web del paso 5, sin `/` al final (la puedes añadir después) |

   Pulsa **Aplicar** y confirma. Para generar la clave en PowerShell:

   ```powershell
   $bytes = New-Object byte[] 48
   [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
   [Convert]::ToBase64String($bytes)
   ```

2. **Configuración** → **Configuración** → **Configuración general**: activa **Credenciales de publicación de autenticación básica de SCM** y guarda. Hace falta para el perfil de publicación.
3. En **Información general** pulsa **Descargar perfil de publicación**. Es un archivo `.PublishSettings`; trátalo como una contraseña y no lo subas al repositorio.
4. Apunta el **Dominio predeterminado** de *Información general* (por ejemplo `https://ordali-api-tunombre.azurewebsites.net`).

## 5. Web: Static Web Apps gratis

1. Busca **Static Web Apps** → **Crear**.
2. Grupo de recursos: `rg-ordali`. Nombre: `ordali-web`. Plan: **Free**.
3. Origen de la implementación: **Otro** (el workflow del repositorio se encarga).
4. **Revisar y crear** → **Crear**.
5. Abre el recurso. En **Información general** está su URL (`https://<algo>.azurestaticapps.net`): ponla en `Cors__AllowedOrigins__0` de la API (paso 4).
6. Pulsa **Administrar token de implementación** y copia el token.

## 6. Conectar GitHub con Azure

En GitHub, abre el repositorio → **Settings** → **Secrets and variables** → **Actions**.

Pestaña **Secrets** → **New repository secret**:

| Nombre | Valor |
| --- | --- |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | El contenido completo del archivo `.PublishSettings` (ábrelo con el Bloc de notas y copia todo) |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | El token de despliegue de Static Web Apps |

Pestaña **Variables** → **New repository variable**:

| Nombre | Valor |
| --- | --- |
| `AZURE_WEBAPP_NAME` | El nombre de la aplicación web, por ejemplo `ordali-api-tunombre` |
| `AZURE_API_URL` | La URL de la API, por ejemplo `https://ordali-api-tunombre.azurewebsites.net` (sin `/` al final) |

Mientras estas variables no existan, el workflow se salta los dos trabajos, así que no falla en forks ni en clones.

## 7. Desplegar

1. En GitHub ve a **Actions** → **Azure** → **Run workflow** (rama `main`).
2. El trabajo `api` pasa los tests, publica la API y espera a que `/health/ready` responda. La primera vez puede tardar unos minutos mientras se crea la base de datos.
3. El trabajo `web` compila la web apuntando a `AZURE_API_URL` y la sube a Static Web Apps.

A partir de ahí, cada push a `main` que toque la API o la web vuelve a desplegar.

Comprueba:

- `https://<tu-api>/swagger`: la documentación de la API.
- `https://<tu-web>.azurestaticapps.net`: la web; entra con `admin@demo.local` / `Demo1234!`.

## Problemas frecuentes

| Síntoma | Causa y solución |
| --- | --- |
| La primera carga tarda o da 503 | La API o la base de datos estaban dormidas. Espera un minuto y recarga. |
| La web dice que no puede conectar con la API y la consola muestra un error de CORS | `Cors__AllowedOrigins__0` no coincide exactamente con la URL de la web (sin `/` final, con `https://`). |
| La API arranca con datos de ejemplo pero se pierden al reiniciar | Falta `ConnectionStrings__Ordali` o tiene otro nombre: la API usa la base de datos en memoria. |
| `Login failed for user` o error al abrir la base de datos | Contraseña mal escrita en la cadena, o *Permitir servicios de Azure* desactivado en las redes del servidor SQL. |
| El despliegue falla con `401 Unauthorized` | Las credenciales de autenticación básica de SCM están desactivadas, o el perfil de publicación es antiguo. Actívalas y vuelve a descargar el perfil. |
| La API responde 403 *Site disabled* | Se ha agotado la CPU diaria del plan F1. Vuelve a funcionar al día siguiente. |
| La base de datos no responde a final de mes | Se han gastado los segundos de vCore gratuitos. Se reactiva el día 1. |

## Borrarlo todo

En el portal, abre el grupo de recursos `rg-ordali` → **Eliminar grupo de recursos**. Se borran la base de datos, la API y la web de una vez. Después borra los secretos y variables del repositorio para que el workflow vuelva a saltarse.
