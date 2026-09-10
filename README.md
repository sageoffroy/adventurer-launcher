# Adventurer Launcher

Launcher de Windows para **Aventureros de Azeroth**.

## Alcance de la v1

La primera versión queda limitada a lo que realmente necesitamos:

- Panel de **Noticias**.
- Panel de **Changelog**.
- Selección y validación de la carpeta de World of Warcraft 3.3.5a.
- Comprobación de los dos parches del cliente por SHA-256.
- Descarga segura mediante archivo temporal antes de reemplazar el parche instalado.
- Botón **Jugar** que ejecuta `Wow.exe` solamente cuando el cliente está actualizado.
- Instalador de Windows mediante Inno Setup.

## Estructura

```text
src/AdventurerLauncher/        aplicación WPF (.NET 8)
installer/                     instalador de Windows
distribution/examples/         formato de manifest, noticias y changelog
.github/workflows/             build automático de Windows
```

## Flujo del jugador

1. Instala `AventurerosLauncherSetup.exe`.
2. En el primer inicio selecciona la carpeta de WoW 3.3.5a.
3. El launcher comprueba que existan `Wow.exe` y `Data/`.
4. Consulta el manifest remoto.
5. Compara SHA-256 de los parches instalados con los publicados.
6. Si hace falta, descarga únicamente los archivos desactualizados.
7. Cuando todo coincide, habilita **Jugar**.

La ruta elegida se guarda en `%LOCALAPPDATA%/AventurerosLauncher/settings.json`.

## Distribución

El código del launcher puede permanecer privado, pero el `manifest.json`, `news.json`, `changelog.json` y los archivos que deban descargar los jugadores necesitan estar disponibles mediante URLs accesibles sin credenciales.

No se guardan nombres inventados para los parches en el código. Sus rutas reales se definen en `manifest.json`, de modo que podemos publicar exactamente los dos patch Z usados por Aventureros de Azeroth cuando fijemos sus nombres y ubicación definitivos.

## Configuración remota

`src/AdventurerLauncher/launcher-config.json` contiene la URL pública del manifest. El valor actual es deliberadamente inválido hasta decidir el punto de publicación definitivo.

Ver `distribution/examples/manifest.json` para el formato esperado.

## Compilar

```powershell
dotnet build src/AdventurerLauncher/AdventurerLauncher.csproj -c Release
```

El workflow `Windows build` también genera automáticamente un build `win-x64` como artifact de GitHub Actions.

## Rama de desarrollo

El desarrollo inicial se realiza en:

```text
feature/launcher-v1
```

`main` queda estable hasta tener una primera versión comprobada.
