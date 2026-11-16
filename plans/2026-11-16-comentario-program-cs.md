# Plan: Comentario descriptivo en Program.cs

Fecha: 2026-11-16
Estado: implementado; verificaciones pendientes

## Objetivo

Añadir al principio de `ApiTUI/ApiTUI/Program.cs` un comentario en español que explique qué hace el fichero.

## Contexto

`Program.cs` es el composition root de la consola (top-level statements): crea el Generic Host, desactiva los proveedores de logging, activa la validación del contenedor DI (`ValidateOnBuild`, `ValidateScopes`), registra servicios (`IHello`, `AddApiTui()`), arranca el host, ejecuta `ApiTuiApp.RunAsync` con el token `ApplicationStopping`, trata la cancelación como salida normal y detiene el host en `finally`. Al final declara `ApiTuiJsonContext` (source generator de System.Text.Json para Native AOT). La documentación del proyecto está en español.

## Pasos

1. Insertar antes de `using ApiTUI;` (línea 1) un bloque de comentarios `//` en español, p. ej.:

   ```csharp
   // Punto de entrada y composition root de ApiTUI.
   // Configura el Generic Host (sin proveedores de logging y con validación del contenedor DI),
   // registra los servicios de la aplicación mediante AddApiTui(), arranca el host y ejecuta
   // ApiTuiApp hasta que se solicita la parada; la cancelación se trata como salida normal.
   // También declara ApiTuiJsonContext, el contexto de System.Text.Json con source generators
   // necesario para la publicación Native AOT.
   ```

2. No modificar ninguna otra línea ni fichero.

## Verificación

- `dotnet build ApiTUI/ApiTUI.slnx --configuration Release` compila sin errores ni nuevos warnings.
- `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes` pasa (sin problemas de formato/cabecera).
- `dotnet test ApiTUI/ApiTUI.slnx` sigue en verde.
- `git diff` muestra solo las líneas de comentario añadidas.

## Riesgos / cosas a vigilar

- Si hay una regla de `.editorconfig` de cabecera de fichero (`file_header_template`), el comentario debe ser compatible con ella.
- Mantener el idioma español y no usar `///` (los XML doc comments no aplican a top-level statements).

## Fuera de alcance

- Refactorizar `Program.cs`, mover `JsonPrueba`/`ApiTuiJsonContext` o eliminar el registro de `IHello`.
- Commit (si se hace, con la identidad `bugbitAgent` según AGENTS.md).
