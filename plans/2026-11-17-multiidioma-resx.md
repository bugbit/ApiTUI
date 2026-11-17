# Plan: Multiidioma con ficheros de recursos (.resx)

Fecha: 2026-11-17
Estado: implementado

## Objetivo
Sustituir los catálogos JSON incrustados (`Localization/es.json`, `en.json`) por ficheros de recursos `.resx` con `ResourceManager`, el mismo mecanismo que usa Terminal.Gui, manteniendo intacto el diseño del dominio (códigos + argumentos) y la API pública de `Messages` (`Get`, `Format`, `Language`).

## Contexto
- Terminal.Gui 2.5.0 localiza con `.resx`: clase `Terminal.Gui.Resources.Strings` (`ResourceManager`, `Culture`) y ensamblados satélite `fr-FR`, `ja-JP`, `pt-PT`, `zh-Hans` (`~/.nuget/packages/terminal.gui/2.5.0/lib/net10.0/<cultura>/Terminal.Gui.resources.dll`). Su idioma neutro es inglés y **no trae español**. Elige idioma según `CultureInfo.CurrentUICulture`.
- Estado actual (sin commit; incluye el trabajo de los planes `2026-11-17-mensajes-dominio-multiidioma.md` y el saludo traducido):
  - `ApiTUI/ApiTUI/Localization/Messages.cs`: carga `ApiTUI.Localization.<idioma>.json` con `GetManifestResourceStream` + `ApiTuiJsonContext.Default.DictionaryStringString`; `Language`, `Get(key, args)`, `Format(Error)`; normaliza `en-US` → `en`; idioma desconocido → `es`; clave desconocida → la clave.
  - `Localization/es.json`, `en.json`: 15 códigos de `Domain/DomainErrorCodes.cs` + `Ui.HelloWorld` (`Localization/UiMessageKeys.cs`).
  - `ApiTUI.csproj`: `<EmbeddedResource Include="Localization\*.json" LogicalName=...>`; `InvariantGlobalization=true`.
  - `Program.cs`: `[JsonSerializable(typeof(Dictionary<string, string>))]` (solo lo usa `Messages`); `AddApiTui(builder.Configuration["Language"])`.
  - `tests/ApiTUI.Domain.Tests/MessagesTests.cs`: helper `Catalog(language)` lee los JSON; tests de completitud, marcadores `{n}`, normalización de idioma, `Get`, `Format`, `Hello`, Generic Host.
- **Restricción clave:** con `InvariantGlobalization=true`, crear `CultureInfo("en")` lanza `CultureNotFoundException` porque `PredefinedCulturesOnly` vale `true` por defecto. `ResourceManager` necesita una `CultureInfo` para buscar el satélite. Solución: añadir `<PredefinedCulturesOnly>false</PredefinedCulturesOnly>` (se mantiene el modo invariante y no hace falta ICU en Linux/Docker; las culturas solo conservan el nombre, que es lo que necesita `ResourceManager`).

## Pasos
1. **`ApiTUI.csproj`**
   - Añadir `<PredefinedCulturesOnly>false</PredefinedCulturesOnly>` junto a `InvariantGlobalization` (que sigue en `true`).
   - Añadir `<NeutralLanguage>es</NeutralLanguage>` (genera `[assembly: NeutralResourcesLanguage("es")]`): el `.resx` sin sufijo es español y no se busca satélite `es`.
   - Eliminar el `<EmbeddedResource Include="Localization\*.json" .../>`. Los `.resx` se incluyen solos (SDK); nombre del recurso resultante: `ApiTUI.Localization.Messages`.
2. **Recursos** (carpeta `ApiTUI/ApiTUI/Localization/`):
   - `Messages.resx` (español, neutro) y `Messages.en.resx` (inglés) con las mismas 16 entradas que hoy tienen los JSON (`name` = código, p. ej. `ApiRequest.NameReserved`; `value` = plantilla `string.Format`). Copiar los textos tal cual.
   - Sin clase generada (`Designer.cs`): las claves ya están en `DomainErrorCodes` y `UiMessageKeys` y el dominio no debe depender de recursos.
   - Borrar `es.json` y `en.json`.
3. **`Localization/Messages.cs`** — reimplementar sobre `ResourceManager`, misma API:
   - `private static readonly ResourceManager Resources = new("ApiTUI.Localization.Messages", typeof(Messages).Assembly);`
   - `public CultureInfo Culture { get; }` (nuevo) y `public string Language { get; }`.
   - Constructor `Messages(string? language)`: normalizar como hoy (trim, minúsculas, parte antes de `-`). Si vacío o `es` → `CultureInfo.InvariantCulture`/neutro y `Language = "es"`. Si no, `CultureInfo.GetCultureInfo(normalized)` dentro de `try/catch (CultureNotFoundException)`; comprobar que existe satélite con `Resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false) is not null`; si no existe o el nombre no es válido → español. No depender de `CultureInfo.Parent` (en modo invariante no es fiable): por eso se normaliza a dos letras antes de crear la cultura.
   - `Get(key, args)`: `Resources.GetString(key, Culture)` (el `ResourceManager` ya cae al neutro español si falta en inglés); si devuelve `null` → la clave. Formatear con `string.Format(CultureInfo.InvariantCulture, …)` como hoy.
   - Quitar `using System.Text.Json` y el método `Load`.
4. **`Program.cs`**
   - Quitar `[JsonSerializable(typeof(Dictionary<string, string>))]` de `ApiTuiJsonContext` (ya nadie lo usa).
   - Tras construir el host, alinear la cultura de interfaz para que Terminal.Gui use el mismo idioma: `var messages = host.Services.GetRequiredService<Messages>(); CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = messages.Culture;` (con `es` Terminal.Gui mostrará sus propios textos en inglés porque no tiene español; queda anotado en riesgos).
5. **Tests (`MessagesTests.cs`)**
   - Reescribir el helper `Catalog(language)` para leer del `ResourceManager`: `new ResourceManager("ApiTUI.Localization.Messages", typeof(Messages).Assembly).GetResourceSet(culture, true, tryParents: false)` y convertir las entradas a `Dictionary<string, string>` (`es` → `CultureInfo.InvariantCulture`, `en` → `CultureInfo.GetCultureInfo("en")`).
   - Mantener el resto de tests (completitud contra `DomainErrorCodes` + `UiMessageKeys`, marcadores `{n}`, normalización `en-US`/` EN-us `/`fr`/`null`/`""`, `Get`, `Format`, `Hello`, Generic Host, cultura invariante en el formato).
   - Añadir: `new Messages("en").Culture.Name == "en"`; un nombre de cultura inválido (p. ej. `"!!"`) → `es` sin excepción.
6. **Documentación**
   - README (párrafo de idioma): las traducciones están en `ApiTUI/ApiTUI/Localization/Messages.resx` (español) y `Messages.en.resx` (inglés), con el mismo mecanismo de recursos que Terminal.Gui; añadir un idioma = añadir `Messages.<idioma>.resx`.
   - En `plans/2026-11-17-mensajes-dominio-multiidioma.md`, nota final: el catálogo JSON se sustituye por `.resx` según este plan.

## Verificación
Desde la raíz del repo:
1. `dotnet build ApiTUI/ApiTUI.slnx -c Release`: se genera `bin/Release/net10.0/en/ApiTUI.resources.dll`.
2. `dotnet test ApiTUI/ApiTUI.slnx` en verde.
3. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`.
4. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64` sin avisos IL2xxx/IL3xxx (en Windows, si falla `vswhere`, añadir `C:\Program Files (x86)\Microsoft Visual Studio\Installer` al `PATH`).
5. **Prueba decisiva de AOT:** ejecutar el binario publicado: sin argumentos imprime `¡Hola, mundo!`; con `--Language en` imprime `Hello, World!`; con `--Language fr` imprime `¡Hola, mundo!`. Si con `en` sale español, el satélite no se está incluyendo en la imagen nativa: investigar (p. ej. comprobar `UseSystemResourceKeys`, que debe ser `false`, y la carpeta `en/` en la salida) antes de dar la tarea por buena.
6. `dotnet run --project ApiTUI/ApiTUI -- --Language en` imprime `Hello, World!`.

## Riesgos / cosas a vigilar
- Native AOT + ensamblados satélite: es el punto a confirmar con la verificación 5. Si el binario nativo no incluye el satélite, **no** desactivar `InvariantGlobalization` ni volver a JSON por cuenta propia: parar e informar.
- `PredefinedCulturesOnly=false` permite crear cualquier cultura con nombre válido; por eso se comprueba que exista el satélite antes de aceptarla.
- Terminal.Gui no tiene español: con `es` sus botones/diálogos propios saldrán en inglés. Aceptado; traducirlos queda fuera de alcance.
- Fijar `CurrentUICulture` afecta solo a la búsqueda de recursos; el formato de mensajes sigue con `InvariantCulture`.
- Los tests que manipulan cultura o consola siguen en la colección `Language configuration` (sin paralelismo).
- No commitear: el árbol contiene trabajo previo sin commit.

## Fuera de alcance
- Traducir al español los textos propios de Terminal.Gui.
- Clase de recursos fuertemente tipada (`Messages.Designer.cs`).
- Cambiar el diseño del dominio (`Error`, `DomainErrorCodes`) o la selección de idioma (`--Language`, `DOTNET_Language`).
- Más idiomas que español e inglés.

## Resultado

- Migradas las 16 traducciones por idioma a `.resx`, conservando los textos. Eliminados los catálogos JSON y su registro en `ApiTuiJsonContext`.
- `Messages` usa `ResourceManager`, mantiene su API y expone `Culture`. El arranque fija `CurrentUICulture` y `DefaultThreadCurrentUICulture`.
- Se mantienen `InvariantGlobalization=true` y `UseSystemResourceKeys=false`; `PredefinedCulturesOnly=false` y `NeutralLanguage=es` están configurados.
- Release sin avisos ni errores; generado `bin/Release/net10.0/en/ApiTUI.resources.dll`. Pasan los 232 tests y la comprobación de formato.
- Native AOT `win-x64` sin avisos IL2xxx/IL3xxx. El ejecutable imprime español sin argumentos, inglés con `en` y `en-US`, y español con `fr` o `!!`. La ejecución administrada con `--Language en` también imprime inglés.
- README, comentario de `UiMessageKeys` y plan anterior actualizados. No se ha cambiado el dominio, el índice de Git ni se han hecho commits.
- No se ha probado la futura interfaz de Terminal.Gui ni una publicación Linux/Docker.
