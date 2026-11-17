# Plan: Mensajes de dominio multiidioma

Fecha: 2026-11-17
Estado: implementado

## Objetivo
Que los errores del dominio se puedan mostrar en varios idiomas (español e inglés) sin que el dominio contenga texto de ningún idioma.

### Propuesta (resumen)
**El dominio no habla ningún idioma: devuelve un código de error estable y sus argumentos; la capa de presentación lo traduce.**
- `Error` pasa de `(Reason, Message)` a `(Reason, Code, Args)`: `Reason` sigue siendo la categoría (`InvalidInput`, `IdNull`), `Code` identifica el mensaje concreto (`ApiRequest.NameEmpty`) y `Args` lleva los datos variables (caracteres no válidos, nombre reservado…).
- Las traducciones viven **en ficheros JSON aparte, uno por idioma** (`Localization/es.json`, `Localization/en.json`: código → plantilla), incrustados en el ejecutable como recursos. La clase `ErrorMessages` los carga con `System.Text.Json` + `ApiTuiJsonContext` y traduce `Code` + `Args` a texto. Lo usa la UI; el dominio no lo conoce.
- El idioma se elige por configuración del Generic Host (`--Language en` o variable `DOTNET_Language`… ver paso 6), por defecto `es`.

Por qué así (decidido con el usuario) y no `.resx`/`ResourceManager`:
- `ApiTUI.csproj` tiene `InvariantGlobalization=true`: en ese modo crear culturas como `es`/`en` lanza `CultureNotFoundException` salvo que se añada `PredefinedCulturesOnly=false`. Con JSON propio no dependemos de `CultureInfo` en absoluto.
- JSON incrustado + source generator es compatible con Native AOT, sin reflexión ni ensamblados satélite; añadir un idioma es añadir un fichero.
- El dominio queda puro: los tests de dominio comprueban códigos, no frases, y cambiar una traducción no rompe tests de dominio.

## Contexto
- `ApiTUI/ApiTUI/Common/Result.cs`: `public sealed record Error(string Reason, string Message)`; `Result<T>.Failure(string reason, string message)`, `Failure(Error)`, conversiones implícitas.
- `ApiTUI/ApiTUI/Common/ErrorsReasons.cs`: `InvalidInput`, `IdNull`.
- 15 llamadas a `Failure(reason, message)` en el dominio:
  - `Domain/ApiRequest.cs` (8, en español; incluye `{InvalidNameChars}` y `{baseName}` interpolados).
  - `Domain/HeaderName.cs` (2; uno interpola `Describe(invalidChars)`).
  - `Domain/HeaderValue.cs` (1), `Domain/VarName.cs` (2; interpola los caracteres no válidos), `Domain/VarValue.cs` (1).
  - `Domain/Identifiers.cs` (1, **en inglés**: `$"{typeof(T).Name} cannot be null or empty."`).
- Nadie fuera del dominio lee `Error.Message` todavía (no hay UI aún). Solo los tests:
  - `tests/ApiTUI.Domain.Tests/ApiRequestTests.cs:146` (mensaje exacto), `HeaderNameTests.cs:67-69,87-88`, `IdentifiersTests.cs:52,66`, `VarNameTests.cs:63`.
- Composition root: `ApiTUI/ApiTUI/Program.cs` + `ServiceCollectionExtensions.AddApiTui()`.
- Reglas: `.claude/skills/csharp-skill/SKILL.md` (sealed, sin `!`, Native AOT, YAGNI: sin interfaz si solo hay una implementación).
- **Estado del árbol:** los cambios de `ApiRequest`/`RequestMethod` (plan `plans/2026-11-17-apirequest-dominio.md`) están en *staging*, sin commit. Este plan se implementa encima; no deshacer ese *staging*.

## Pasos
1. **`Common/Result.cs`** — cambiar `Error`:
   ```csharp
   public sealed record Error(string Reason, string Code, IReadOnlyList<object> Args)
   {
       public Error(string reason, string code, params object[] args) : this(reason, code, (IReadOnlyList<object>)args) { }
   }
   ```
   (o un único constructor con `params object[] args`, lo que resulte más simple y compile sin ambigüedad). `Result<T>.Failure(string reason, string message)` → `Failure(string reason, string code, params object[] args)`. Eliminar `Message`.
   - Igualdad del record: `Args` es una lista (igualdad por referencia). Aceptable; los tests comparan `Code`/`Args` con `Assert.Equal` sobre la secuencia.
2. **`Domain/DomainErrorCodes.cs`** (nuevo, `public static class`, `const string`), un código por mensaje actual, con formato `Tipo.Motivo`:
   - `Identifier.Required` (arg: nombre del tipo, `typeof(T).Name`).
   - `ApiRequest.IdRequired`, `ApiRequest.LocationRequired`, `ApiRequest.NameEmpty`, `ApiRequest.NameInvalidChars` (arg: `InvalidNameChars`), `ApiRequest.NameEndsWithDot`, `ApiRequest.NameReserved` (arg: `baseName`), `ApiRequest.MethodInvalid`, `ApiRequest.HeaderNull`.
   - `HeaderName.Empty`, `HeaderName.InvalidChars` (arg: texto de `Describe(invalidChars)`, que es neutro respecto al idioma).
   - `HeaderValue.NameRequired`.
   - `VarName.Empty`, `VarName.InvalidChars` (arg: lista de caracteres `'$', '@'`).
   - `VarValue.NameRequired`.
3. **Dominio** — sustituir cada `Failure(reason, "texto")` por `Failure(reason, DomainErrorCodes.X, args...)` en `ApiRequest.cs`, `HeaderName.cs`, `HeaderValue.cs`, `VarName.cs`, `VarValue.cs`, `Identifiers.cs`. Mantener la misma `Reason` que hoy. Ningún texto en lenguaje natural debe quedar en el dominio.
4. **Ficheros de traducción** (nuevos, carpeta `ApiTUI/ApiTUI/Localization/`):
   - `es.json` y `en.json`: objeto plano `{ "ApiRequest.NameReserved": "El nombre de la petición no puede ser un nombre reservado del sistema ({0}).", ... }`. Plantillas `string.Format` (`{0}`…). Los textos en español son los actuales; el inglés es su traducción. Recordar escapar `{`/`}` literales como `{{`/`}}` si aparecen.
   - En `ApiTUI.csproj`: `<EmbeddedResource Include="Localization\*.json" LogicalName="ApiTUI.Localization.%(Filename)%(Extension)" />` (nombre lógico fijo, independiente de la carpeta).
   - En `ApiTuiJsonContext` (`Program.cs`): añadir `[JsonSerializable(typeof(Dictionary<string, string>))]`.
5. **`Localization/ErrorMessages.cs`** (nuevo, `public sealed class`, sin interfaz: una sola implementación, YAGNI):
   - Constructor `ErrorMessages(string? language)`: normaliza el idioma (minúsculas, parte antes de `-`: `en-US` → `en`); si no hay recurso `ApiTUI.Localization.<idioma>.json` o viene vacío/`null` → `es`.
   - Carga con `typeof(ErrorMessages).Assembly.GetManifestResourceStream(nombre)` y `JsonSerializer.Deserialize(stream, ApiTuiJsonContext.Default.DictionaryStringString)`. Carga siempre `es` como respaldo. Si el recurso `es` falta o no es JSON válido, lanzar `InvalidOperationException` (es un error de compilación del producto, no del usuario; lo detectan los tests).
   - `public string Format(Error error)`: busca `Code` en el idioma elegido → si falta, en `es` → si falta, devuelve el propio `Code`. Formatea con `string.Format(CultureInfo.InvariantCulture, plantilla, error.Args.ToArray())`.
   - `public string Language { get; }` con el idioma finalmente usado.
6. **Selección de idioma** — en `Program.cs`/`AddApiTui()` registrar `ErrorMessages` como singleton leyendo `builder.Configuration["Language"]` (lectura directa, sin `ConfigurationBinder`, compatible con AOT). Así funciona con `dotnet run --project ApiTUI/ApiTUI -- --Language en` y con la variable `DOTNET_Language=en`. Cuando exista `settings.json`, se añadirá como fuente de configuración (fuera de alcance).
7. **Tests**
   - Tests de dominio existentes: cambiar las aserciones sobre `Error.Message` por `Error.Code` (y `Error.Args` donde se comprobaban caracteres): `ApiRequestTests.cs:146`, `HeaderNameTests.cs:67-69,87-88`, `IdentifiersTests.cs:52,66`, `VarNameTests.cs:63`. En el resto de tests de error añadir la comprobación de `Code` donde aporte.
   - Nuevo `tests/ApiTUI.Domain.Tests/ErrorMessagesTests.cs` (el proyecto de tests ya referencia `ApiTUI`; si en el futuro existe `ApiTUI.Tests`, se moverá):
     - **Completitud:** todos los `const` de `DomainErrorCodes` (por reflexión en el test, permitido porque los tests no se publican en AOT) tienen plantilla en `es.json` y en `en.json`, y ningún JSON tiene claves que no existan en `DomainErrorCodes`.
     - Las plantillas de cada idioma usan los mismos marcadores `{n}` que la de `es`, y `string.Format` no lanza con ellas.
     - `new ErrorMessages("en").Language == "en"` (el recurso incrustado se encuentra).
     - `Format` con `es` y `en` devuelve el texto esperado para un caso con argumentos (p. ej. `ApiRequest.NameReserved` con `CON`).
     - Idioma desconocido (`fr`), `null` y `en-US` → comportamiento descrito en el paso 5.
     - Código inexistente → devuelve el código.
8. **Documentación** — README: mencionar en funcionalidades/configuración que la interfaz admite español e inglés y cómo elegir idioma (`--Language`, `DOTNET_Language`). Añadir en `plans/2026-11-17-apirequest-dominio.md` que el pendiente de idioma de mensajes queda resuelto por este plan.

## Verificación
Desde la raíz del repo:
1. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64` sin avisos IL2xxx/IL3xxx (en Windows, si falla `vswhere`, añadir `C:\Program Files (x86)\Microsoft Visual Studio\Installer` al `PATH`).
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`
3. `dotnet test ApiTUI/ApiTUI.slnx` (todos en verde, incluidos los nuevos de `ErrorMessagesTests`).
4. `grep -rn "Failure(ErrorsReasons\.[A-Za-z]*, \"" ApiTUI/ApiTUI/Domain` y `grep -rn '\$"' ApiTUI/ApiTUI/Domain` no devuelven mensajes en lenguaje natural.

## Riesgos / cosas a vigilar
- Comprobar en la publicación AOT que los JSON incrustados están en el ejecutable (p. ej. ejecutar el binario publicado con `--Language en` cuando haya forma de mostrar un error; mientras tanto basta el test de `Language == "en"` y la ausencia de avisos IL).
- `InvariantGlobalization=true` debe seguir activado: el diseño no depende de `CultureInfo` de idiomas concretos. No cambiarlo.
- `Args` como `object[]`: solo valores simples (`string`, `char`, números). No pasar objetos de dominio cuyo `ToString()` cambie.
- Los mensajes con listas de caracteres (`HeaderName`, `VarName`) llevan la lista ya formateada como argumento; la parte «Solo se admiten…» pasa a la plantilla traducida.
- Cambiar la firma de `Failure`/`Error` rompe la compilación en todos los usos a la vez: hacerlo en un solo paso (pasos 1–3 juntos) y compilar.
- No commitear mezclando con el trabajo en *staging* de `ApiRequest` sin que el usuario lo pida.

## Fuera de alcance
- Traducir los textos de la UI (menús, etiquetas): se hará cuando exista la UI, reutilizando el mismo mecanismo si conviene.
- `settings.json` y cambiar el idioma en caliente desde la interfaz.
- Más idiomas que español e inglés.
- Ficheros de traducción externos en la carpeta de datos (editables sin recompilar).

## Resultado

- Migrados los 15 errores del dominio a códigos estables y argumentos. `Error.Message` se ha eliminado; los argumentos se copian y exponen como solo lectura.
- Añadidos catálogos JSON incrustados en español e inglés, cargados con `ApiTuiJsonContext`, y `ErrorMessages` con respaldo en español.
- Registrado el traductor como singleton mediante `AddApiTui(language)`. Probadas la variable `DOTNET_Language` y la prioridad de `--Language`.
- README y pendiente de idioma del plan de `ApiRequest` actualizados. No se afirma que exista una UI traducida: sigue fuera de alcance.
- Verificados 220 tests (21 nuevos), formato y Native AOT `win-x64` sin avisos. El ejecutable publicado arranca con `es`, `en`, `en-US` y `fr`.
- El árbol real al empezar no tenía cambios en staging; solo este plan estaba sin seguimiento. No se ha cambiado el índice ni se han hecho commits.

## Migración posterior a recursos

Los catálogos JSON se sustituyen por `Localization/Messages.resx` y `Messages.en.resx` según `plans/2026-11-17-multiidioma-resx.md`. La clase actual es `Messages`, compartida por errores de dominio y textos de interfaz. El dominio mantiene sus códigos y argumentos.
