# Plan: Configurar la inyección de dependencias en ApiTUI

Fecha: 2026-11-15
Estado: implementado y verificado

## Objetivo
Dotar a la aplicación de consola `ApiTUI` de un composition root basado en Generic Host (`Microsoft.Extensions.Hosting`) donde registrar los servicios, compatible con Native AOT y sin interferir con la futura TUI.

## Contexto
- `ApiTUI/ApiTUI/Program.cs` solo contiene `Console.WriteLine("Hello, World!");` y el proyecto no tiene paquetes de DI.
- Decisiones tomadas con el usuario:
  - Base: **Generic Host**. Aporta DI, configuración (servirá después para la carpeta de datos: CLI → variable de entorno → carpeta del SO), logging y Options. Es compatible con Native AOT.
  - Alcance: **solo el proyecto de consola**. Las capas Domain/Application/Infrastructure se crearán cuando tengan código (YAGNI, según `csharp-skill`).
- `ApiTUI/ApiTUI/ApiTUI.csproj` tiene `PublishAot=true`: nada de reflexión no compatible con trimming.
- Reglas de `.claude/skills/csharp-skill/SKILL.md`: clases `sealed`, `CancellationToken` en todo método async, constructores primarios con campo `readonly _x`. **No** copiar el `in` del ejemplo del skill: rompe la resolución de DI (`Unable to resolve service for type 'X&'`, verificado).
- La TUI ocupará la consola: ningún logger debe escribir en ella.

## Pasos
1. **Paquete.** Desde `ApiTUI/`:
   `dotnet add ApiTUI/ApiTUI.csproj package Microsoft.Extensions.Hosting`
   (versión 10.x, alineada con `net10.0`).

2. **Clase de aplicación**: nuevo `ApiTUI/ApiTUI/ApiTuiApp.cs`.
   - `public sealed class ApiTuiApp` con `Task RunAsync(CancellationToken cancellationToken)`.
   - De momento mantiene el comportamiento actual (escribe "Hello, World!"); será el punto donde se arranque Terminal.Gui más adelante.

3. **Registro de servicios**: nuevo `ApiTUI/ApiTUI/ServiceCollectionExtensions.cs`.
   - `public static IServiceCollection AddApiTui(this IServiceCollection services)` que registra `ApiTuiApp` como singleton.
   - Será el único sitio donde la consola registra sus servicios; cuando existan las capas, cada una aportará su propio `AddXxx()`.

4. **Composition root**: reescribir `ApiTUI/ApiTUI/Program.cs`.
   ```csharp
   var builder = Host.CreateApplicationBuilder(args);
   builder.Logging.ClearProviders(); // la TUI es dueña de la consola
   builder.ConfigureContainer(new DefaultServiceProviderFactory(
       new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
   builder.Services.AddApiTui();

   using var host = builder.Build();
   await host.StartAsync();
   var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
   await host.Services.GetRequiredService<ApiTuiApp>().RunAsync(lifetime.ApplicationStopping);
   await host.StopAsync();
   ```
   - No se usa `host.Run()` ni un `BackgroundService`: Terminal.Gui debe ejecutarse en el hilo principal.
   - `StartAsync` activa `ConsoleLifetime`, así Ctrl+C dispara `ApplicationStopping` y llega como `CancellationToken`.
   - `ValidateOnBuild`/`ValidateScopes` siempre activos (por defecto solo en Development) para que un registro roto falle al arrancar.

5. **Documentación**: en `AGENTS.md`, sección «Arquitectura prevista», añadir una línea indicando que el composition root es `Program.cs` (Generic Host) y que los servicios se registran mediante métodos de extensión `AddXxx()`.

6. **Verificación** (comprobaciones de `csharp-skill`, desde la raíz del repo):
   1. `dotnet run --project ApiTUI/ApiTUI` → imprime "Hello, World!" y termina sin salida extra del host.
   2. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64` sin avisos `IL2xxx`/`IL3xxx` (en Windows, con `C:\Program Files (x86)\Microsoft Visual Studio\Installer` en el `PATH`). Ejecutar el binario publicado y comprobar la misma salida.
   3. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`
   4. `dotnet test ApiTUI/ApiTUI.slnx`

## Riesgos / cosas a vigilar
- Si algún logger queda activo, sus mensajes corromperán la TUI cuando se añada Terminal.Gui.
- El binding de configuración a clases de Options necesitará `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` para ser AOT-safe; no se añade ahora porque aún no hay Options.
- El ejemplo del skill con `in` en el constructor primario no debe copiarse.

## Fuera de alcance
- Crear los proyectos Domain/Application/Infrastructure.
- Añadir Terminal.Gui, cliente HTTP o persistencia.
- Opciones de configuración (carpeta de datos) y su binding.
- Tests de la composición del contenedor (el `ValidateOnBuild` cubre los registros al arrancar).

## Resultado de la implementación

- Paquete fijado en `Microsoft.Extensions.Hosting` 10.0.15.
- `ApiTuiApp` registrado como singleton mediante `AddApiTui()`, con validación del contenedor siempre activa y proveedores de logging desactivados.
- El arranque pasa `ApplicationStopping` a la aplicación, trata la cancelación del host como salida normal y detiene el host en un `finally`.
- README y AGENTS actualizados con el estado y las rutas actuales.
- Verificado el 2026-11-15: ejecución con `dotnet run`, publicación Native AOT `win-x64` sin avisos IL2xxx/IL3xxx, ejecutable nativo con salida exacta `Hello, World!` y código 0, formato sin cambios y tests (1 prueba vacía superada).
- Las comprobaciones .NET requirieron ejecución fuera del sandbox por restricciones de acceso a tuberías.
- Pendiente para la integración de Terminal.Gui: verificar el hilo de ejecución. Continuar tras `await host.StartAsync()` no garantiza por sí solo conservar el hilo principal. Ctrl+C no se ha probado de forma interactiva porque la aplicación actual termina inmediatamente.
