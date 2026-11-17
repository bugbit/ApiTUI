# Plan: Entidad de dominio ApiRequest y método HTTP QUERY

Fecha: 2026-11-17
Estado: implementado

## Objetivo
1. Crear el tipo `RequestMethod` con los métodos HTTP soportados, incluido **`QUERY`** (método seguro e idempotente con cuerpo, publicado en junio de 2026 como [RFC 10008](https://www.rfc-editor.org/rfc/rfc10008.html)).
2. Crear la entidad `ApiRequest`: una petición con identificador, nombre, método, URL, cabeceras, cuerpo y su ubicación: está guardada **o bien** directamente en una colección (`ApiCollectionId`) **o bien** en una carpeta (`ApiFolderId`), exactamente una de las dos.

## Contexto
- Value objects de dominio ya existentes en `ApiTUI/ApiTUI/Domain/`:
  - `Identifiers.cs`: `ApiRequestId`, `ApiCollectionId` y `ApiFolderId` (`Create(string?)` → `Result<T>`).
  - `StringWithVars.cs`: texto anulable con `{{var}}`.
  - `HeaderValue.cs`: `Name` (`HeaderName`), `Value` (`StringWithVars`), `Enabled`.
- `Result<T>` y `ErrorsReasons` (`InvalidInput`, `IdNull`) en `ApiTUI/ApiTUI/Common/`.
- README («Gestión de peticiones») lista los métodos `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS`; hay que añadir `QUERY`.
- Reglas `.claude/skills/csharp-skill/SKILL.md`: `sealed`, sin `!`, sin anulables en lo obligatorio, Native AOT, YAGNI.
- Se evita el nombre `HttpMethod` para no chocar con `System.Net.Http.HttpMethod` (que se usará en infraestructura).

## Pasos
1. Crear `ApiTUI/ApiTUI/Domain/RequestMethod.cs`:
   ```csharp
   public enum RequestMethod
   {
       Get, Post, Put, Patch, Delete, Head, Options, Query,
   }
   ```
   - `Get` es el primer valor (valor por defecto del enum).
   - Sin conversión a `System.Net.Http.HttpMethod` aquí: es responsabilidad de infraestructura (`new HttpMethod("QUERY")`).
2. Crear `ApiTUI/ApiTUI/Domain/ApiRequest.cs`, namespace `ApiTUI.Domain`:
   ```csharp
   public sealed class ApiRequest
   {
       public ApiRequestId Id { get; }
       public string Name { get; }
       public RequestMethod Method { get; }
       public StringWithVars Url { get; }
       public IReadOnlyList<HeaderValue> Headers { get; }
       public StringWithVars Body { get; }
       public ApiCollectionId? CollectionId { get; }   // exactamente uno de los dos
       public ApiFolderId? FolderId { get; }           // tiene valor

       private ApiRequest(...) { ... }

       // Petición guardada directamente en una colección.
       public static Result<ApiRequest> CreateInCollection(
           ApiRequestId? id,
           ApiCollectionId? collectionId,
           string? name,
           RequestMethod method = RequestMethod.Get,
           StringWithVars? url = null,
           IEnumerable<HeaderValue?>? headers = null,
           StringWithVars? body = null)

       // Petición guardada en una carpeta.
       public static Result<ApiRequest> CreateInFolder(
           ApiRequestId? id,
           ApiFolderId? folderId,
           string? name,
           RequestMethod method = RequestMethod.Get,
           StringWithVars? url = null,
           IEnumerable<HeaderValue?>? headers = null,
           StringWithVars? body = null)

       // Ambas delegan en un private static Create(id, collectionId, folderId, ...) con la validación común.
   }
   ```
   Reglas comunes de creación (cada fallo devuelve `Result.Failure`, nunca lanza):
   - `id` `null` → `ErrorsReasons.IdNull`, «El identificador de la petición es obligatorio.».
   - `name` nulo, vacío o solo espacios → `InvalidInput`; se guarda recortado (`Trim()`).
   - `method` no definido en el enum (p. ej. `(RequestMethod)99`, comprobado con `Enum.IsDefined`) → `InvalidInput`.
   - `url` `null` → `new StringWithVars(null)`; puede estar vacía (borrador sin URL; se valida al enviar).
   - `headers` `null` → lista vacía; algún elemento `null` → `InvalidInput`. Se copia a un array (`ToArray()`) para que cambios en la colección original no afecten a la entidad. Se permiten nombres repetidos (HTTP admite cabeceras repetidas) y se conserva el orden.
   - `body` `null` → `new StringWithVars(null)`.
   - Ubicación: con dos factorías explícitas es imposible pedir «colección y carpeta a la vez». `collectionId` `null` en `CreateInCollection` o `folderId` `null` en `CreateInFolder` → `ErrorsReasons.IdNull`. Por tanto siempre se cumple el invariante: exactamente uno de `CollectionId`/`FolderId` tiene valor. No se comprueba aquí que la colección/carpeta exista (capa de aplicación/persistencia). Son los únicos anulables intencionados de la entidad.
   - Es una **entidad**: `sealed class` (no `record`), igualdad por referencia por defecto; las ediciones se modelarán en una tarea posterior.
3. Crear tests en `ApiTUI/tests/ApiTUI.Domain.Tests/`:
   - `RequestMethodTests.cs`: el enum contiene exactamente los 8 métodos, incluido `Query`; `default(RequestMethod) == RequestMethod.Get`.
   - `ApiRequestTests.cs` (helpers para crear `ApiRequestId`, `HeaderName`, `HeaderValue`):
     - `CreateInCollection` completa conserva todas las propiedades (`CollectionId` igual, `FolderId` `null`); método por defecto `Get`.
     - `CreateInFolder` conserva `FolderId` y deja `CollectionId` en `null`.
     - Cada método del enum (`[Theory]`, incluido `Query`) es válido.
     - Las reglas comunes (nombre, método, url, body, headers) se prueban con `CreateInCollection`, y al menos un caso de nombre inválido con `CreateInFolder` para asegurar que ambas factorías comparten validación.
     - `(RequestMethod)99` → `InvalidInput`.
     - `id` `null` → `IdNull` (en ambas factorías).
     - `collectionId` `null` en `CreateInCollection` y `folderId` `null` en `CreateInFolder` → `IdNull`.
     - `name` `null`, `""`, `"   "` → `InvalidInput`; `"  Login  "` → `"Login"`.
     - `url`/`body` `null` → no nulos con `Text == null`; `{{baseUrl}}/login` se conserva.
     - `headers` `null` → vacía; con un `null` dentro → `InvalidInput`; orden y repetidos conservados; modificar la lista original después de crear no cambia `Headers`.
4. Actualizar `README.md` («Gestión de peticiones»): añadir `QUERY` a la lista de métodos HTTP.
5. Al terminar, cambiar en este plan `Estado: pendiente de implementar` por `Estado: implementado`.

## Verificación
Desde la raíz del repo:
1. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64` (sin avisos IL2xxx/IL3xxx).
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`
3. `dotnet test ApiTUI/ApiTUI.slnx` (y `--filter "FullyQualifiedName~ApiRequestTests|FullyQualifiedName~RequestMethodTests"`).

## Riesgos / cosas a vigilar
- `Enum.IsDefined<RequestMethod>(method)` (genérico) es compatible con AOT; no usar la sobrecarga con `Type`.
- `IEnumerable<HeaderValue?>` permite que el test pase un `null` dentro de la lista sin usar `!`.
- Invariante colección XOR carpeta garantizado por las factorías; si en el futuro aparece un `Move...`, debe mantenerlo.
- No exponer la lista mutable: exponer `IReadOnlyList<HeaderValue>` respaldada por un array copiado y protegido con `Array.AsReadOnly`.
- `QUERY` está publicado como RFC 10008; algunos servidores no lo admiten y responderán 405/501. Es un resultado HTTP normal, no un error de la app.
- Al serializar a JSON el enum habrá que usar `JsonStringEnumConverter<RequestMethod>` (compatible con AOT); queda fuera de esta tarea.

## Fuera de alcance
- Parámetros de consulta como lista editable (por ahora forman parte del texto de `Url`).
- Tipos de cuerpo (JSON, texto, form-data), autenticación, scripts.
- Comprobar que la colección/carpeta existe; mover la petición entre colecciones o carpetas.
- Métodos de edición de `ApiRequest`, ejecución HTTP y conversión a `System.Net.Http.HttpMethod`.
- Serialización JSON y persistencia.

## Resultado

- Implementados `ApiRequest`, `RequestMethod` y sus tests. README actualizado con `QUERY`.
- Cabeceras copiadas y protegidas con `Array.AsReadOnly`, para impedir que un consumidor modifique el array mediante un cast.
- Corregidos dos espacios ausentes en `ErrorsReasons.IdNull`: el fallo de formato ya existía antes de implementar.
- Verificado: 32 casos específicos y 176 tests totales superados; Native AOT `win-x64` sin avisos y formato correcto.

## Ajustes tras la revisión

- `ApiRequest` implementa `IEquatable<ApiRequest>` con igualdad por `Id` (también `==`, `!=` y `GetHashCode`), como corresponde a una entidad. Sustituye a la igualdad por referencia.
- El nombre se valida como nombre de fichero válido en todos los SO soportados (se usa en `requests/<Petición>.json`): sin caracteres de control ni `<>:"/\|?*`, sin punto final y sin nombres reservados de Windows (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`, también con extensión).
- Idioma de mensajes resuelto mediante `plans/2026-11-17-mensajes-dominio-multiidioma.md`: el dominio devuelve códigos y argumentos; la presentación obtiene los textos de catálogos JSON en español e inglés.
- Verificado: 199 tests superados, formato correcto y Native AOT `win-x64` sin avisos.
