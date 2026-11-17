# Plan: Caso de uso «Enviar ApiRequest»

Fecha: 2026-11-17
Estado: implementado

## Objetivo
Crear el caso de uso que envía una `ApiRequest` por HTTP y devuelve la respuesta (estado, cabeceras, cuerpo y duración) como `Result`, sin acoplar la capa de aplicación a `HttpClient`. Las variables `{{var}}` se resuelven con un diccionario `IReadOnlyDictionary<VarName, object?>` que **de momento solo se crea vacío** dentro del caso de uso (los entornos llegarán después).

## Contexto
- Dominio existente (`ApiTUI/ApiTUI/Domain/`):
  - `ApiRequest`: `Method` (`RequestMethod`, incluido `Query`), `Url` y `Body` (`StringWithVars`), `Headers` (`IReadOnlyList<HeaderValue>` con `Name` `HeaderName`, `Value` `StringWithVars`, `Enabled`).
  - `StringWithVars.Resolve(IReadOnlyDictionary<VarName, object?>)`: sustituye `{{nombre}}`; las variables que no están en el diccionario se quedan como `{{nombre}}`.
  - `VarName.Create(string?)` valida nombres (`a-zA-Z0-9-_.`).
- Patrón puertos/adaptadores ya usado: `Application/Ports/IIdGenerator.cs` ↔ `Adapters/Identity/GuidIdGenerator.cs`, registrados en `ServiceCollectionExtensions.AddApiTui()`.
- Errores: `Result<T>`, `Error(Reason, Code, Args)` (`Common/Result.cs`), `ErrorsReasons` (`Common/ErrorsReasons.cs`), códigos de dominio en `Domain/DomainErrorCodes.cs`, traducciones en `Localization/Messages.resx` y `Messages.en.resx`. `tests/ApiTUI.Domain.Tests/MessagesTests.cs` exige que los recursos contengan exactamente las constantes de `DomainErrorCodes` + `UiMessageKeys`.
- Reglas (`.claude/skills/csharp-skill/SKILL.md` y `AGENTS.md`): `sealed`, `record` para datos, constructores primarios con campo `_x`, todo método asíncrono recibe y propaga `CancellationToken`, sin `!`, Native AOT, YAGNI; los errores de conexión/configuración se muestran en la interfaz y no rompen la app.
- Todos los tests están hoy en un único proyecto `tests/ApiTUI.Domain.Tests` (ya contiene tests de adaptadores y localización). Se sigue esa práctica.
- Árbol limpio; último commit `a4ef5dc`.

## Pasos
1. **Dominio — `StringWithVars`**: añadir `public IReadOnlyList<VarName> GetVarNames()` que devuelve, sin repetir y en orden de aparición, los nombres válidos de `{{...}}` del texto (reutilizando `VarPattern()` y `VarName.Create`). Lo usa el caso de uso para detectar variables sin valor. Tests en `StringWithVarsTests.cs` (texto nulo/vacío → vacío, repetidos, nombre inválido ignorado).
2. **Aplicación — modelos** (`Application/SendApiRequest/`):
   - `HttpRequestData` (`sealed record`): `RequestMethod Method`, `Uri Url`, `IReadOnlyList<KeyValuePair<string, string>> Headers`, `string? Body`. Es la petición ya resuelta que recibe el puerto.
   - `ApiResponse` (`sealed record`): `int StatusCode`, `string? ReasonPhrase`, `IReadOnlyList<KeyValuePair<string, string>> Headers` (cabeceras de respuesta + de contenido; se admiten repetidas), `string Body`, `TimeSpan Duration`.
3. **Aplicación — puerto** `Application/Ports/IHttpSender.cs`:
   ```csharp
   Task<Result<HttpResponseData>> SendAsync(HttpRequestData request, CancellationToken cancellationToken);
   ```
   con `HttpResponseData` (`sealed record`: `StatusCode`, `ReasonPhrase`, `Headers`, `Body`) en la misma carpeta que los modelos. La duración la mide el caso de uso, no el adaptador.
4. **Aplicación — códigos de error** `Application/ApplicationErrorCodes.cs` (`public static class`, `const string`):
   - `SendApiRequest.UnresolvedVariables` (arg: nombres separados por `, `), `SendApiRequest.UrlEmpty`, `SendApiRequest.UrlInvalid` (arg: URL), `SendApiRequest.UrlSchemeNotSupported` (arg: esquema), `SendApiRequest.HeaderInvalid` (arg: nombre de cabecera), `SendApiRequest.HeaderValueInvalid` (arg: nombre de cabecera).
   - `Http.Timeout` (arg: segundos del límite), `Http.RequestFailed` (arg: `HttpRequestError` como texto, p. ej. `NameResolutionError`).
   - En `ErrorsReasons` añadir `Timeout` y `HttpFailure`. Las validaciones de URL/variables/cabeceras usan `InvalidInput`.
   - Añadir las 8 entradas a `Messages.resx` (es) y `Messages.en.resx` (en), y en `MessagesTests` incluir `ApplicationErrorCodes` en la comprobación de completitud y en la lista de códigos con argumento (todos salvo `UrlEmpty`).
5. **Aplicación — caso de uso** `Application/SendApiRequest/SendApiRequestUseCase.cs`:
   ```csharp
   public sealed class SendApiRequestUseCase(IHttpSender httpSender, TimeProvider timeProvider)
   {
       public Task<Result<ApiResponse>> ExecuteAsync(ApiRequest request, CancellationToken cancellationToken);
   }
   ```
   - `ArgumentNullException.ThrowIfNull(request)`; `cancellationToken.ThrowIfCancellationRequested()`.
   - **Diccionario de variables:** `var variables = new Dictionary<VarName, object?>();` en un método privado `CreateVariables()` con comentario: de momento vacío; más adelante se rellenará con el entorno activo.
   - Recoger los `GetVarNames()` de `Url`, `Body` y del `Value` de las cabeceras **habilitadas**; los que no estén en el diccionario → `InvalidInput` / `SendApiRequest.UnresolvedVariables`. (Con el diccionario vacío, cualquier `{{var}}` produce este error: es el comportamiento esperado por ahora.)
   - Resolver la URL: vacía o en blanco → `UrlEmpty`; `Uri.TryCreate(url, UriKind.Absolute, out var uri)` falla → `UrlInvalid`; esquema distinto de `http`/`https` → `UrlSchemeNotSupported`.
   - Cabeceras: solo `Enabled`, en orden, con el valor resuelto (`null` → `""`). **Tras resolver**, si el valor contiene `\r`, `\n` o `\0` → `InvalidInput` / `SendApiRequest.HeaderValueInvalid` con el nombre de la cabecera, y no se llama al puerto (evita inyección de cabeceras: `TryAddWithoutValidation` no rechaza esos caracteres y `HeaderValue` tampoco los valida, porque el valor puede llegar de una variable). La validación se hace después de resolver para cubrir también los valores que vendrán de variables.
   - Cuerpo: resuelto; vacío → `null` (no se envía contenido).
   - Medir: `var start = _timeProvider.GetTimestamp();` → `SendAsync` → `_timeProvider.GetElapsedTime(start)`.
   - Si el puerto devuelve error, propagarlo tal cual. Si va bien, construir `ApiResponse` con la duración.
   - **Cancelación del usuario:** no se convierte en `Result`; la `OperationCanceledException` se propaga (convención .NET; la UI la tratará como «cancelado»).
6. **Infraestructura — adaptador** `Adapters/Http/HttpClientSender.cs` (`sealed`, `IHttpSender`, constructor primario con `HttpClient`):
   - `RequestMethod` → `HttpMethod` (`Query` → `new HttpMethod("QUERY")`; el resto, las propiedades estáticas de `HttpMethod`).
   - Cuerpo no nulo → `ByteArrayContent(Encoding.UTF8.GetBytes(body))` (sin `Content-Type` por defecto, para no imponer `text/plain`).
   - Cabeceras: defensa en profundidad, el adaptador también rechaza valores con `\r`, `\n` o `\0` → `HeaderValueInvalid` (el puerto es público y no debe confiar en el llamador). Después `request.Headers.TryAddWithoutValidation`; si falla (cabeceras de contenido como `Content-Type`), `content.Headers.TryAddWithoutValidation` si hay contenido; si tampoco se puede → `InvalidInput` / `SendApiRequest.HeaderInvalid`.
   - **Límite de tiempo de toda la operación (cabeceras + cuerpo):** no depender de `HttpClient.Timeout` (con `ResponseHeadersRead` solo cubre hasta recibir las cabeceras y la lectura del cuerpo podría quedarse esperando indefinidamente). El adaptador aplica su propio límite:
     - Recibe un `HttpSenderOptions` (`sealed record HttpSenderOptions(TimeSpan Timeout)`, por defecto 100 s) y `TimeProvider`.
     - `using var timeoutCts = new CancellationTokenSource(_options.Timeout, _timeProvider);` y `using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);`.
     - `SendAsync(message, HttpCompletionOption.ResponseHeadersRead, linked.Token)` **y** `ReadAsStringAsync(linked.Token)` usan el token enlazado, así el límite cubre la respuesta completa.
     - El `HttpClient` registrado tiene `Timeout = Timeout.InfiniteTimeSpan` para que el único límite sea el del adaptador.
   - Cabeceras de respuesta: `response.Headers` y `response.Content.Headers` aplanadas (un par por valor).
   - Errores:
     - `HttpRequestException ex` → `HttpFailure` / `Http.RequestFailed` con `ex.HttpRequestError.ToString()`.
     - `OperationCanceledException` con `cancellationToken.IsCancellationRequested` (cancelación del usuario) → **relanzar**, aunque el límite también haya vencido.
     - `OperationCanceledException` con `timeoutCts.IsCancellationRequested` y el token del usuario sin cancelar → `Timeout` / `Http.Timeout` con los segundos del límite.
   - Disponer `HttpRequestMessage` y `HttpResponseMessage` (`using`).
7. **Registro DI** (`ServiceCollectionExtensions.AddApiTui`):
   - `AddSingleton(TimeProvider.System)`.
   - `AddSingleton(new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) }) { Timeout = Timeout.InfiniteTimeSpan })` (un único `HttpClient` de larga vida; sin `Microsoft.Extensions.Http` por YAGNI; el límite lo pone el adaptador).
   - `AddSingleton(new HttpSenderOptions(TimeSpan.FromSeconds(100)))`.
   - `AddSingleton<IHttpSender, HttpClientSender>()` y `AddSingleton<SendApiRequestUseCase>()`. `ValidateOnBuild` ya comprueba que todo se resuelve.
8. **Tests** (en `tests/ApiTUI.Domain.Tests`, con dobles escritos a mano, sin red):
   - `SendApiRequestUseCaseTests.cs` con un `FakeHttpSender` (guarda la `HttpRequestData` recibida y devuelve un resultado configurado) y un `FakeTimeProvider` (subclase de `TimeProvider` que sobrescribe `GetTimestamp`/`TimestampFrequency`):
     - Petición sin variables → `HttpRequestData` con método, URL, solo cabeceras habilitadas en orden, cuerpo; `ApiResponse` con datos del puerto y `Duration` medida.
     - Valor de cabecera habilitada con `\r`, `\n` o `\0` (`[Theory]`, p. ej. `"a\r\nX-Injected: 1"`) → `HeaderValueInvalid` con el nombre de la cabecera y el puerto **no** se llama; el mismo valor en una cabecera deshabilitada → no es error.
     - `{{baseUrl}}` en URL, cabecera habilitada o cuerpo → `UnresolvedVariables` y el puerto **no** se llama; variable solo en cabecera deshabilitada → no es error.
     - URL vacía, relativa, con esquema `ftp` → códigos correspondientes; puerto no llamado.
     - Cuerpo vacío → `Body == null`.
     - Error del puerto → se devuelve el mismo `Error`.
     - Token ya cancelado → `OperationCanceledException`.
   - `HttpClientSenderTests.cs` con un `HttpMessageHandler` falso:
     - Todos los `RequestMethod` producen el método HTTP esperado (`Query` → `"QUERY"`).
     - `Content-Type` va a las cabeceras de contenido; cabeceras normales a la petición; sin cuerpo no hay contenido.
     - Respuesta: estado, `ReasonPhrase`, cabeceras (incluidas las de contenido) y cuerpo.
     - `HttpRequestException(HttpRequestError.NameResolutionError, …)` → `Http.RequestFailed` con ese argumento.
     - Límite de tiempo (con `HttpSenderOptions` de ~200 ms y `TimeProvider.System`; sin `Task.Delay` fijos en el test, solo esperas que terminan por cancelación):
       - El handler no responde (espera `Task.Delay(Timeout.Infinite, token)`) → `Http.Timeout`.
       - El handler devuelve cabeceras pero el cuerpo es un `Stream` cuyo `ReadAsync` espera hasta que se cancela el token → `Http.Timeout` (demuestra que el límite cubre la lectura del cuerpo).
       - El usuario cancela su token mientras se lee el cuerpo → se propaga `OperationCanceledException`, no `Http.Timeout`.
     - Valor de cabecera con `\r\n` llamando directamente al adaptador → `HeaderValueInvalid` y el handler **no** recibe la petición.
     - Cabecera que no admite ni la petición ni el contenido → `HeaderInvalid`.
   - `MessagesTests`: completitud con `ApplicationErrorCodes` (paso 4).
9. **Documentación**: README, en «Estado del proyecto», indicar que existe el caso de uso de envío (sin interfaz todavía) y que las variables aún no tienen valores (cualquier `{{var}}` da error hasta que existan los entornos).

## Verificación
Desde la raíz del repo:
1. `dotnet build ApiTUI/ApiTUI.slnx -c Release` sin avisos.
2. `dotnet test ApiTUI/ApiTUI.slnx` en verde (nuevos tests incluidos).
3. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`.
4. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64` sin avisos IL2xxx/IL3xxx (en Windows, si falla `vswhere`, añadir `C:\Program Files (x86)\Microsoft Visual Studio\Installer` al `PATH`); el binario sigue arrancando (`¡Hola, mundo!`), lo que confirma que `ValidateOnBuild` resuelve los nuevos servicios.

## Riesgos / cosas a vigilar
- Distinguir *timeout* de cancelación del usuario: solo es `Http.Timeout` si `cancellationToken.IsCancellationRequested` es `false` y `timeoutCts.IsCancellationRequested` es `true`.
- El límite debe cubrir cabeceras **y** cuerpo (revisión de Codex): cualquier lectura de la respuesta usa el token enlazado.
- Inyección de cabeceras (revisión de Codex): `TryAddWithoutValidation` no rechaza CR/LF/NUL; se validan en el caso de uso y en el adaptador.
- Tests de tiempo: límites cortos (~200 ms) y esperas que dependen de la cancelación, para que no sean lentos ni inestables.
- `TryAddWithoutValidation` acepta casi todo; el caso `HeaderInvalid` es raro (p. ej. cabecera de contenido sin cuerpo). No inventar validaciones adicionales.
- Cuerpos de respuesta muy grandes se leen enteros en memoria; aceptado por ahora.
- `HttpRequestError.ToString()` funciona en AOT; no usar reflexión adicional.
- No mezclar responsabilidades: el caso de uso no conoce `HttpClient`; el adaptador no resuelve variables ni mide tiempos.
- No commitear salvo que el usuario lo pida.

## Fuera de alcance
- Rellenar el diccionario de variables (entornos, variables de colección, `settings.json`).
- Interfaz Terminal.Gui para enviar y mostrar la respuesta; formato legible de JSON.
- Tamaño de la respuesta, redirecciones configurables, certificados, proxy, autenticación.
- Separar los tests en proyectos por capa (`ApiTUI.Application.Tests`, etc.).

## Resultado

- Añadidos `GetVarNames`, modelos de envío/respuesta, puerto `IHttpSender`, `SendApiRequestUseCase` y adaptador `HttpClientSender`.
- Se rechazan CR/LF/NUL tanto después de resolver cabeceras en el caso de uso como al acceder directamente al adaptador. El límite de 100 segundos cubre envío y lectura del cuerpo; la cancelación del usuario se propaga como excepción.
- Añadidos ocho códigos de error con traducciones en español e inglés y completitud verificada en los recursos.
- Registrados los nuevos servicios. Desviación respecto al ejemplo: `HttpClient` se registra mediante una factoría singleton para que DI lo disponga al cerrar el contenedor.
- README y estado de AGENTS actualizados con la implementación actual y sus límites.
- Verificados Release y Native AOT `win-x64` sin avisos, formato correcto y 283 tests superados (51 casos nuevos). El ejecutable AOT arranca con saludo en español y código 0.
- No se han hecho conexiones de red, pruebas de TUI, publicaciones para otros sistemas ni commits.
