# Ordali para escritorio y Android (.NET MAUI)

La misma interfaz que la web, empaquetada como app nativa con .NET MAUI Blazor Hybrid para Windows, macOS y Android. Las pantallas vienen de `Ordali.Ui`; este proyecto solo añade el arranque, la sesión en el almacén seguro del sistema y una pantalla de ajustes.

En iPhone se usa la web instalada como PWA, así que no hay build de iOS.

## Descargar y probar

Cada versión publicada en **Releases** del repositorio incluye:

| Archivo | Plataforma | Cómo abrirlo |
| --- | --- | --- |
| `Ordali-android.apk` | Android 7 o superior | Ábrelo en el móvil y permite instalar apps de origen desconocido cuando lo pida. |
| `Ordali-windows-x64.zip` | Windows 10 u 11 | Descomprime y ejecuta `Ordali.exe`. Si SmartScreen avisa, pulsa **Más información → Ejecutar de todas formas**. |
| `Ordali-macos.zip` | macOS 14 o superior | Descomprime y abre `Ordali.app`. La primera vez macOS lo bloquea: ve a **Ajustes del Sistema → Privacidad y seguridad** y pulsa **Abrir igualmente**. |

Las apps no están firmadas con un certificado de pago, de ahí los avisos.

Al abrirla arranca en **modo demo**, con datos de ejemplo y sin servidor. Usuarios (contraseña `Demo1234!`):

- `admin@demo.local`: administrador.
- `mozo@demo.local`: mozo.

## Conectarla a la API

1. Arranca la API con `docker compose up --build` en la raíz del repositorio.
2. En la app, abre **Ajustes**, elige **Mi API de Ordali** y escribe la URL:
   - En el mismo ordenador: `http://localhost:8080/`
   - En el emulador de Android: `http://10.0.2.2:8080/`
   - En un móvil en la misma wifi: la IP local del ordenador, por ejemplo `http://192.168.1.20:8080/`
3. Pulsa **Guardar**. La app comprueba que la API responde; ciérrala y vuelve a abrirla para usarla.

## Compilar

Requisitos: .NET 10 SDK y el workload de MAUI de cada plataforma.

```bash
# Android (en Windows, macOS o Linux; necesita el SDK de Android y un JDK 17 o superior)
dotnet workload install maui-android
dotnet build src/Ordali.App -f net10.0-android -t:Run

# Windows
dotnet workload install maui-windows
dotnet build src/Ordali.App -f net10.0-windows10.0.19041.0 -t:Run

# macOS (necesita Xcode)
dotnet workload install maui-maccatalyst
dotnet build src/Ordali.App -f net10.0-maccatalyst -t:Run
```

Si no tienes el SDK de Android, `dotnet build src/Ordali.App -f net10.0-android -t:InstallAndroidDependencies -p:AndroidSdkDirectory=<carpeta> -p:AcceptAndroidSDKLicenses=true` lo descarga.

## Publicar una versión

El workflow `.github/workflows/apps.yml` compila las tres apps en cada PR que toca este proyecto. Al subir una etiqueta, además crea la release con los tres archivos:

```bash
git tag v0.1.0
git push origin v0.1.0
```

El número de la etiqueta pasa a ser la versión de la app. También se puede hacer sin terminal: en **Actions → Apps → Run workflow**, escribe la versión (por ejemplo `0.1.0`) y el workflow crea la etiqueta y la release.

## Detalles

| Tema | Decisión |
| --- | --- |
| Sesión | El JWT se guarda con `SecureStorage` (Keystore en Android, Llavero en macOS, DPAPI en Windows), no en el `localStorage` del WebView. |
| HTTP en Android | `network_security_config.xml` permite HTTP sin cifrar porque la API de demo corre en local. En producción iría solo por HTTPS. |
| APK | Formato APK en lugar de AAB, para instalarlo directamente; incluye arm64 (móviles) y x64 (emulador). |
| Icono | `Resources/AppIcon/ordali_logo.png` es el logo sin fondo; MAUI genera con él el icono y la pantalla de carga de cada plataforma sobre el azul `#0B1323`. |
| Windows | Sin empaquetar y autocontenido: no hace falta instalar .NET ni un certificado. |
