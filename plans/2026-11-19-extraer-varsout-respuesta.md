# Plan: Extraer VarsOut de la respuesta HTTP

Fecha: 2026-11-19
Estado: pendiente de implementar

## Objetivo
Cuando `SendApiRequestUseCase` recibe una respuesta y la petición tiene `VarsOut`, evaluar cada `VarValue` de `VarsOut` contra la respuesta y escribir el resultado en la variable de destino ya existente. Los fallos de un VarOut se anotan y se continúa con el siguiente; la petición sigue siendo un éxito.

Ejemplos (cuerpo de respuesta → valor asignado):
- `{{response}}` con `hola` → `var1 = "hola"` (texto completo del cuerpo).
- `{{response.json.prop1}}` con `{ "prop1": 2 }` → `var2 = "2"`.
- `{{response.json.prop2.prop21}}` con `{ "prop2": { "prop21": "world" } }` → `var3 = "world"`.
- `X-API-Key {{response.json.token}}` con `{ "token": "apikey1" }` → `var4 = "X-API-Key apikey1"` (plantilla con texto fijo).

## Contexto
- `ApiTUI/ApiTUI/Domain/ApiRequest.cs` ya expone `IReadOnlyList<VarValue>? VarsOut` (sin nombres repetidos gracias a `VarValueList`).
- `VarName` admite `.`, así que `response.json.a.b` ya se reconoce como marcador con `StringWithVars.GetVarNames()` / `Resolve(...)` (`Domain/StringWithVars.cs`), que se pueden reutilizar para sustituir la plantilla.
- `SendApiRequestUseCase.LoadVariablesAsync` (`Application/SendApiRequest/SendApiRequestUseCase.cs`) aplica las variables en este orden de precedencia: colección < entorno < carpeta raíz < … < carpeta de la petición. Solo cuentan las variables `Enabled`.
- Hoy solo hay puertos de lectura (`IApiCollectionReader`, `IApiEnvironmentReader`, `IApiFolderReader`), implementados por `Adapters/Persistence/InMemoryApiStore.cs`. Las entidades son inmutables.
- `Result<T>` es una unión `(Error, T)`. `MessagesTests` exige que todos los códigos de `ApplicationErrorCodes` tengan traducción en `Messages.resx` y en `Messages.en.resx`.

Decisiones tomadas con el usuario:
- **Si un VarOut falla:** se registra el error y se sigue. La respuesta se devuelve igualmente.
- **Variable de destino:** tiene que existir ya, con el mismo nombre y `Enabled`, en la cadena de variables de la petición. Si no existe, se registra el error y se sigue.
- **Valores JSON:** solo se admiten escalares (string, número, bool y null). Los objetos y arrays son un error y no se admiten índices de array.

## Pasos
1. **Dominio: actualizar un lote de variables** (sin modificar la entidad original)
   - `Domain/VarValueList.cs`: añadir `ReplaceValues(IReadOnlyList<VarValue> list, IReadOnlyDictionary<VarName, StringWithVars> values)`.
     - Devuelve un `record VarValuesReplacement(IReadOnlyList<VarValue> Variables, IReadOnlyList<VarName> Missing)`.
     - Sustituye solo las variables que existen y están `Enabled`, y conserva el orden y `Enabled`.
     - En `Missing` van los nombres que no se han podido sustituir.
   - Añadir `WithVariableValues(IReadOnlyDictionary<VarName, StringWithVars>)` a `ApiCollection`, `ApiEnvironment` y `ApiFolder`.
     - Devuelve la nueva instancia (mismo Id, nombre y ubicación) junto con los nombres de `Missing`.

2. **Puertos de escritura atómica por ámbito** (`Application/Ports/`)
   - El caso de uso **no** guarda entidades completas leídas antes del envío HTTP. Pide al almacén que aplique el lote sobre la entidad *vigente*.
   - Crear `IApiCollectionWriter`, `IApiEnvironmentWriter` e `IApiFolderWriter`, cada uno con:
     `Task<Result<IReadOnlyList<VarName>>> UpdateVariablesAsync(<Id> id, IReadOnlyDictionary<VarName, StringWithVars> values, CancellationToken ct)`.
     - Si la entidad ya no existe → `Error` con el código de "no encontrado" correspondiente (`CollectionNotFound`, `EnvironmentNotFound` o `FolderNotFound`).
     - Si existe, el valor es la lista de nombres que la entidad vigente ya no contiene (`Missing`).
   - Implementación en `InMemoryApiStore`:
     - Bucle de lectura, cálculo y `TryUpdate` (compare-and-swap) sobre el `ConcurrentDictionary`: se lee la entidad vigente, se aplica `WithVariableValues` y se reintenta si otra escritura cambió la entidad entre medias.
     - Así ninguna actualización concurrente se pierde, aunque cambie otras variables de la misma entidad.
     - La función de actualización es pura, así que se puede reintentar sin riesgo.
   - Registrar los writers en `ServiceCollectionExtensions.AddApiTui()` apuntando a la misma instancia singleton, igual que los lectores.

3. **Extractor puro** (`Application/SendApiRequest/ResponseVarExtractor.cs`, estático)
   - Firma: `Result<string> Evaluate(StringWithVars template, string body, Lazy<JsonDocument?> json)`.
   - Marcadores:
     - `response`: el cuerpo tal cual.
     - `response.json.<p1>.<p2>…`: se recorre el JSON por nombre de propiedad, distinguiendo mayúsculas y minúsculas.
   - Valor final:
     - string: sin comillas.
     - número: `GetRawText()`.
     - bool: `true` o `false`.
     - null: cadena vacía.
     - objeto o array: error `VarOutValueNotScalar`.
   - Errores, con nuevos códigos en `ApplicationErrorCodes`:
     - `VarOutResponseNotJson`: el cuerpo no es JSON válido.
     - `VarOutPathNotFound`: no existe la ruta. Los argumentos son la ruta.
     - `VarOutValueNotScalar`.
     - `VarOutPlaceholderNotSupported`: cualquier marcador que no sea `response` ni `response.json.*`. Lo mismo para `response.json` sin ruta.
     - `VarOutTargetNotFound`: no existe la variable de destino. Se usa en el paso 4.
   - El JSON se parsea como mucho una vez por respuesta, con `JsonDocument.Parse` (compatible con AOT; no usa reflexión) y se libera (`Dispose`) al terminar.
   - La sustitución se hace con `StringWithVars.Resolve`, pasándole un diccionario con los marcadores ya calculados.

4. **Caso de uso** (`SendApiRequestUseCase`)
   - El constructor recibe además los tres writers. Hay que actualizar `ApiTuiAppTests`, `SendApiRequestUseCaseTests` y `MessagesTests` si construyen el caso de uso.
   - `LoadVariablesAsync` devuelve, además del diccionario, el *ámbito ganador* de cada `VarName`.
     - El ámbito ganador es el **identificador** de la entidad (colección, entorno o carpeta) que aportó el valor efectivo `Enabled`, según la misma precedencia.
     - Se guarda solo el identificador, no la entidad, para no reescribir después una versión antigua.
   - Después de recibir la respuesta, si `request.VarsOut` no es null ni está vacía, se procesan sus elementos en orden:
     - Se omiten los que tengan `Enabled == false`.
     - Si el nombre no tiene ámbito ganador, se registra `VarOutTargetNotFound`.
     - Se evalúa la plantilla. Si falla, se registra el error.
     - Si va bien, el valor se añade al lote de su ámbito: un diccionario `VarName → StringWithVars` por cada identificador de ámbito.
   - Por último se llama a `UpdateVariablesAsync` **una vez por ámbito** con su lote.
     - Si devuelve `Error` (el ámbito ya no existe), se registra ese error para cada variable del lote.
     - Los nombres que vuelvan en `Missing` se registran como `VarOutTargetNotFound`, porque la variable se borró o se deshabilitó entre la carga y la escritura.
     - El resto de variables del lote se añaden a `AssignedVars`.
     - Si la llamada lanza una excepción que no sea de cancelación, se registra `VarOutSaveFailed` para cada variable del lote. `OperationCanceledException` se propaga.
   - Las VarsOut se procesan para cualquier código HTTP, no solo los 2xx.

5. **Resultado** (`Application/SendApiRequest/ApiResponse.cs`)
   - Nuevo `record VarOutError(VarName Name, Error Error)`.
   - Se añaden a `ApiResponse` las propiedades `IReadOnlyList<VarValue> AssignedVars` e `IReadOnlyList<VarOutError> VarOutErrors`. Ambas son listas vacías cuando no hay VarsOut.

6. **Localización**
   - Añadir los mensajes de los nuevos códigos en `Localization/Messages.resx` (es) y `Messages.en.resx` (en).
   - Añadirlos también a la lista de `MessagesTests` si la mantiene explícitamente.
   - Opcional y pequeño: en `ApiTuiApp`, mostrar las variables asignadas y los errores, con nuevas claves en `UiMessageKeys`.

7. **Tests** (`tests/ApiTUI.Domain.Tests`)
   - `ResponseVarExtractorTests`:
     - Los 4 ejemplos del objetivo.
     - Número, bool, null y string con caracteres especiales.
     - Cuerpo que no es JSON.
     - Ruta inexistente.
     - Objeto o array como valor final.
     - Marcador no soportado.
     - `response.json` sin ruta.
     - Plantilla sin marcadores, que se queda como texto literal.
   - `VarValueListTests`, `ApiCollectionTests`, `ApiEnvironmentTests` y `ApiFolderTests`: comprobar `ReplaceValues` y `WithVariableValues`.
     - La entidad original no cambia.
     - Se conservan el orden y `Enabled`.
     - `Missing` contiene los nombres inexistentes o deshabilitados.
   - `InMemoryApiStoreTests`:
     - `UpdateVariablesAsync` aplica el lote sobre la entidad vigente y devuelve `Missing`.
     - Si la entidad no existe → `Error`.
     - Muchas actualizaciones concurrentes (`Parallel.ForEachAsync`/`Task.WhenAll`) de variables distintas en la misma colección → todas quedan aplicadas.
   - `SendApiRequestUseCaseTests`:
     - Se actualiza la variable en el ámbito ganador (por ejemplo, carpeta por encima de colección).
     - Destino inexistente → error registrado, la respuesta es un éxito y las demás VarsOut se asignan.
     - VarOut deshabilitado → se ignora.
     - Varias VarsOut sobre el mismo ámbito → una sola llamada a `UpdateVariablesAsync` con todo el lote.
     - **Regresión de concurrencia:**
       - Dos ejecuciones del caso de uso sobre la misma colección (con `token` e `id`). Un `IHttpSender` falso espera en una barrera (`TaskCompletionSource`) hasta que **ambas** peticiones han cargado sus variables, así las dos parten de la misma versión inicial.
       - Una petición tiene `VarsOut` para `token` y la otra para `id`.
       - Al liberar la barrera, se comprueba en el almacén que la colección tiene **los dos** valores nuevos.
     - Variable borrada o deshabilitada entre la carga y la escritura → `VarOutTargetNotFound` y el resto del lote sí se aplica.
     - `VarsOut` null o vacía → no se escribe nada.
     - Respuesta no-2xx → también se extrae.

## Verificación
- `dotnet build ApiTUI/ApiTUI.slnx --configuration Release` sin advertencias.
- `dotnet test ApiTUI/ApiTUI.slnx`.
- `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`.
- Publicación Native AOT para win-x64 sin advertencias IL2xxx/IL3xxx (`csharp-skill`).

## Riesgos / cosas a vigilar
- **Variable presente en varios ámbitos:** solo se actualiza el ámbito ganador. Los demás conservan su valor.
- **Texto con `{{x}}` en el valor extraído:** si contiene algo como `{{x}}`, se interpretará como marcador en peticiones posteriores. Se acepta y se documenta en el test.
- **Liberación del `JsonDocument`:** hay que hacer `Dispose` del documento y no conservar `JsonElement` fuera de su vida útil.
- **Precedencia:** no alterar la precedencia ni la semántica de errores existentes del envío (variables sin resolver, URL, etc.).
- **Cancelación:** propagar el `CancellationToken` a `UpdateVariablesAsync`.
- **Concurrencia:** no reintroducir el patrón «leer la entidad antes del HTTP y guardarla entera después». Cualquier futuro adaptador de persistencia (JSON en disco) tiene que mantener la semántica atómica de leer, modificar y escribir por ámbito, con un bloqueo por fichero o una comparación de versión.
- **Reintentos del compare-and-swap:** un bucle con `TryUpdate` puede reintentar si hay mucha contención. Es aceptable en memoria; la función de actualización no debe tener efectos secundarios.

## Fuera de alcance
- Persistencia JSON real en disco (solo el almacén en memoria).
- Índices de array, JSONPath, extracción de cabeceras o del código de estado.
- Crear variables que no existan.
- La UI con Terminal.Gui.
