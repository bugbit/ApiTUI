# AGENTS.md

Este fichero orienta a los agentes de código (Codex, Claude Code, etc.) que trabajan en este repositorio.

## Contexto del proyecto

ApiTUI es un cliente HTTP interactivo para terminal (estilo Postman, con estética Turbo Vision) en C#/.NET 10 con [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui). Es el Trabajo Fin de Máster (TFM) del Máster de Desarrollo con IA de BIG school; los requisitos de entrega están en `Docs/Documentacion-TFM.md`.

**Estado actual:** existe la solución `ApiTUI/ApiTUI.slnx`, una consola .NET 10 con Generic Host y tests de dominio, aplicación, adaptadores y localización. `SendApiRequestUseCase` envía peticiones mediante `IHttpSender` y `HttpClientSender`; como prueba provisional, la consola hace un GET a una API pública y muestra el resultado (es/en). El código vive en carpetas dentro de la consola; los proyectos por capa y la TUI siguen pendientes. El README distingue el estado actual del diseño previsto.

La documentación y el README están en español; mantén ese idioma en la documentación del proyecto.

## Comandos

Desde la raíz del repositorio:

```bash
dotnet restore ApiTUI/ApiTUI.slnx
dotnet build ApiTUI/ApiTUI.slnx --configuration Release
dotnet run --project ApiTUI/ApiTUI
dotnet test ApiTUI/ApiTUI.slnx
dotnet test ApiTUI/tests/ApiTUI.Domain.Tests --filter "FullyQualifiedName~NombreDelTest"

# Publicación Native AOT (un RID por publicación: win-x64, linux-x64, linux-arm64, osx-arm64, osx-x64)
dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r linux-x64 --self-contained true \
  -p:PublishAot=true -p:PublishSingleFile=true -o ./publish/linux-x64

# Docker (la TUI exige terminal interactiva: -it obligatorio)
docker build -t apitui:latest .
docker run --rm -it -v "${PWD}/apitui-data:/data" apitui:latest
```

## Arquitectura prevista

El composition root actual es `ApiTUI/ApiTUI/Program.cs` (Generic Host). La consola registra sus servicios mediante `AddApiTui()`; cada capa incorporará su método `AddXxx()` cuando exista.

Capas separadas para que HTTP y persistencia no se acoplen a las vistas de Terminal.Gui:

- `src/ApiTUI` — punto de entrada y vistas Terminal.Gui (solo UI).
- `src/ApiTUI.Application` — casos de uso que coordinan operaciones.
- `src/ApiTUI.Domain` — modelos (colecciones, carpetas, peticiones, entornos) y reglas.
- `src/ApiTUI.Infrastructure` — cliente HTTP, persistencia JSON y configuración.
- `tests/ApiTUI.{Domain,Application,Infrastructure}.Tests` — un proyecto de tests por capa.

### Persistencia

Datos de usuario fuera del código, un fichero JSON por entidad:

```text
data/
├── collections/<Colección>/collection.json
├── collections/<Colección>/requests/<Petición>.json
├── environments/<Entorno>.json
└── settings.json
```

Prioridad prevista para resolver la carpeta de datos: opción CLI → variable de entorno → carpeta de datos de usuario del SO. En Docker la ruta es `/data`.

### Restricciones de diseño

- **Native AOT es objetivo de publicación**: evita reflexión no compatible con trimming. Usa `System.Text.Json` con source generators (`JsonSerializerContext`) en lugar de serialización basada en reflexión, y comprueba que las dependencias son AOT-compatibles.
- La UI debe funcionar íntegramente con teclado y adaptarse al tamaño de la terminal; los atajos deben seguir las convenciones de Terminal.Gui.
- Las peticiones HTTP deben poder cancelarse (propaga `CancellationToken`) y los errores de conexión/configuración se muestran en la interfaz, no rompen la app.

## Commits

Todo el código generado por IA se commitea con la identidad `bugbitAgent`, sin cambiar la configuración global de git:

```bash
git -c user.name='bugbitAgent' -c user.email='oscar2522@gmail.com' commit -m '<mensaje>'
```

## Requisitos de entrega del TFM

El README debe mantener: descripción general, stack tecnológico, instalación y ejecución, estructura del proyecto y funcionalidades principales. También debe incluir la URL de despliegue/publicación y el enlace a las slides cuando existan.
