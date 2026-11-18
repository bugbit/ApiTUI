# Plan: Resolver variables de entorno, colección y carpetas en SendApiRequestUseCase

Fecha: 2026-11-18
Estado: implementado

## Objetivo
`SendApiRequestUseCase.ExecuteAsync` recibe un `ApiEnvironmentId?` y resuelve los `{{var}}` de la petición con las variables leídas en este orden, donde **lo leído después prevalece**:

1. Entorno (solo si `environmentId` no es `null`).
2. Colección de la petición.
3. Carpetas, desde la carpeta raíz de la colección y bajando por los hijos hasta la carpeta de la petición. La carpeta de la petición es la última y la más específica.

## Contexto
- Hoy `SendApiRequestUseCase.CreateVariables()` (en `Application/SendApiRequest/SendApiRequestUseCase.cs`) devuelve un diccionario vacío. Cualquier `{{var}}` termina en `SendApiRequest.UnresolvedVariables`.
- El commit `7216939` ya tiene `ApiEnvironment`, `ApiCollection` y `ApiFolder` con `Variables: IReadOnlyList<VarValue>`. `VarValue` tiene `Name` (`VarName`), `Value` (`StringWithVars`) y `Enabled`.
- Ubicación en la jerarquía:
  - `ApiRequest` tiene exactamente uno de `CollectionId` o `FolderId`.
  - `ApiFolder` tiene exactamente uno de `CollectionId` (carpeta raíz) o `ParentFolderId` (subcarpeta).
  - Por tanto, la colección de una petición dentro de una carpeta se obtiene subiendo por los padres hasta la carpeta raíz.
- Aún no hay persistencia. Decisión del usuario: **puertos de lectura y un almacén en memoria** que se sustituirá por la persistencia JSON. Si una entidad referenciada no existe, el resultado es **error** y la petición no se envía.
- `Program.cs` valida el contenedor al arrancar (`ValidateOnBuild`), así que todo puerto nuevo necesita un registro en `ServiceCollectionExtensions.AddApiTui()`.
- Patrones a reutilizar:
  - Puertos en `Application/Ports/` (`IHttpSender`), con adaptadores en `Adapters/<Área>/` (`Adapters/Http/HttpClientSender.cs`).
  - Constructor primario con campos `_readonly`, según `.claude/skills/csharp-skill/SKILL.md`.
  - `Result<T>` y `ErrorsReasons` en `Common/`.
  - Los códigos de aplicación van en `Application/ApplicationErrorCodes.cs`, con su texto en los `.resx`.

## Pasos

1. **Puertos** (nuevos, en `ApiTUI/ApiTUI/Application/Ports/`). Devuelven `null` cuando la entidad no existe:
   ```csharp
   public interface IApiEnvironmentReader { Task<ApiEnvironment?> FindAsync(ApiEnvironmentId id, CancellationToken cancellationToken); }
   public interface IApiCollectionReader  { Task<ApiCollection?>  FindAsync(ApiCollectionId id,  CancellationToken cancellationToken); }
   public interface IApiFolderReader      { Task<ApiFolder?>      FindAsync(ApiFolderId id,      CancellationToken cancellationToken); }
   ```
   Cada interfaz va en su propio fichero.

2. **Adaptador en memoria** `ApiTUI/ApiTUI/Adapters/Persistence/InMemoryApiStore.cs`:
   - `sealed class InMemoryApiStore : IApiEnvironmentReader, IApiCollectionReader, IApiFolderReader`.
   - Usa un `ConcurrentDictionary` por tipo, con clave en su Id. Es seguro porque se registra como singleton y se usa desde código asíncrono.
   - `Add(ApiEnvironment)`, `Add(ApiCollection)` y `Add(ApiFolder)`: insertan o reemplazan por Id y lanzan `ArgumentNullException` si reciben `null`.
   - `FindAsync`: comprueba la cancelación con `cancellationToken.ThrowIfCancellationRequested()` y devuelve `Task.FromResult(...)`.
   - Comentario: es provisional hasta que exista la persistencia JSON.

3. **Registro en DI** (`ServiceCollectionExtensions.AddApiTui`):
   - `AddSingleton<InMemoryApiStore>()`.
   - Las tres interfaces se registran apuntando a esa **misma instancia**, por ejemplo `AddSingleton<IApiCollectionReader>(sp => sp.GetRequiredService<InMemoryApiStore>())`, y lo mismo para las otras dos.

4. **`SendApiRequestUseCase`**:
   - El constructor añade `IApiEnvironmentReader`, `IApiCollectionReader` e `IApiFolderReader`, cada uno en un campo `_readonly`.
   - Nueva firma: `ExecuteAsync(ApiRequest request, ApiEnvironmentId? environmentId, CancellationToken cancellationToken)`.
   - `CreateVariables()` se sustituye por `private async Task<Result<Dictionary<VarName, object?>>> LoadVariablesAsync(ApiRequest request, ApiEnvironmentId? environmentId, CancellationToken cancellationToken)`:
     1. `variables = new Dictionary<VarName, object?>()`.
     2. Si `environmentId` no es `null`:
        - `FindAsync`;
        - si devuelve `null` → `Failure(InvalidInput, SendApiRequest.EnvironmentNotFound, environmentId.Id)`;
        - si no, `Apply(environment.Variables)`.
     3. Cadena de carpetas: si `request.FolderId is { } folderId`, sube por los padres:
        - Lleva un `HashSet<ApiFolderId>` de carpetas visitadas y guarda cada carpeta en una lista `chain`.
        - Carpeta no encontrada → `SendApiRequest.FolderNotFound` (arg: Id).
        - Carpeta ya visitada → `SendApiRequest.FolderCycle` (arg: Id). Así se evita un bucle infinito si los datos guardados forman un ciclo.
        - Cuando `folder.ParentFolderId` es `null`, la colección es `folder.CollectionId`. Se obtiene con un patrón `is { } id`, sin `!`.
        - Si no, la colección es `request.CollectionId`. Ambas ramas usan patrones `is { }`; el caso imposible de no tener ninguno devuelve también `CollectionNotFound`, sin lanzar excepción.
     4. Colección: `FindAsync`; si devuelve `null` → `SendApiRequest.CollectionNotFound` (arg: Id). Si no, `Apply(collection.Variables)`.
     5. Invierte `chain` (de la raíz a la hoja) y aplica `Apply(folder.Variables)` a cada carpeta.
     - `Apply`: recorre las variables con `Enabled == true` y asigna `variables[v.Name] = v.Value.Text`, de modo que la última asignación gana. Las deshabilitadas se ignoran y **no** borran un valor anterior.
     - Se propaga el `cancellationToken` a cada `FindAsync`.
   - En `ExecuteAsync`, se llama a `LoadVariablesAsync` antes de comprobar las variables no resueltas y se propaga su error. El resto del flujo no cambia.
   - Comentario breve sobre la precedencia: entorno < colección < carpeta raíz < … < carpeta de la petición.

5. **Códigos y traducciones**:
   - En `ApplicationErrorCodes`: `EnvironmentNotFound = "SendApiRequest.EnvironmentNotFound"`, `CollectionNotFound = "SendApiRequest.CollectionNotFound"`, `FolderNotFound = "SendApiRequest.FolderNotFound"` y `FolderCycle = "SendApiRequest.FolderCycle"`. Todos llevan `{0}` con el Id.
   - `Messages.resx` (es): «No existe el entorno {0}.», «No existe la colección {0}.», «No existe la carpeta {0}.», «La jerarquía de carpetas contiene un ciclo en {0}.»
   - `Messages.en.resx` (en): «The environment {0} does not exist.», «The collection {0} does not exist.», «The folder {0} does not exist.», «The folder hierarchy contains a cycle at {0}.»
   - `MessagesTests.withArgument`: añade los cuatro códigos.

6. **`ApiTuiApp`** (demo provisional):
   - Recibe `InMemoryApiStore`. Esta dependencia del adaptador concreto se admite solo porque la demo es provisional.
   - Crea con `ApiCollection.Create` la colección de la demo, usando el `collectionId` generado y el nombre «Demo». La añade al almacén y llama a `ExecuteAsync(request, null, cancellationToken)`.
   - Si crear la colección falla, se muestra el error con `WriteError`, como en el resto de la demo.

7. **Tests** (`ApiTUI/tests/ApiTUI.Domain.Tests/`):
   - `SendApiRequestUseCaseTests.cs`:
     - Actualiza el constructor y las llamadas con un `InMemoryApiStore` real y una colección registrada. Un helper `CreateUseCase(sender, store)` evita repetirlo.
     - Casos nuevos (comprobando la URL y el cuerpo que recibe `FakeHttpSender`):
       - Variable solo en el entorno se resuelve.
       - Sin `environmentId`, las variables del entorno no se usan → `UnresolvedVariables`.
       - Precedencia: la misma variable en entorno, colección, carpeta raíz y subcarpeta → gana la subcarpeta. Si se quita de la subcarpeta, gana la raíz; si se quita de las carpetas, gana la colección.
       - Petición en una subcarpeta de dos niveles: usa las variables de toda la cadena y de la colección a la que llega la raíz.
       - Una variable deshabilitada en la carpeta no tapa el valor de la colección.
       - Errores, sin llamar al sender (`sender.Requests` vacío): `EnvironmentNotFound`, `CollectionNotFound`, `FolderNotFound` y `FolderCycle`. El ciclo A → B → A se construye con `CreateInFolder` y se añade al almacén.
       - Un `CancellationToken` cancelado antes de la lectura lanza `OperationCanceledException`.
   - `InMemoryApiStoreTests.cs` (nuevo):
     - `Add` y luego `FindAsync` devuelven la misma instancia.
     - Un Id inexistente devuelve `null`.
     - `Add` con el mismo Id reemplaza la entidad.
     - Un token cancelado lanza.
   - `ApiTuiAppTests.cs`: ajusta la construcción con el almacén y el nuevo constructor del caso de uso.
   - `MessagesTests.cs`: comprueba que el contenedor de `AddApiTui` sigue validando y que existen los códigos nuevos.

8. **README**:
   - En «Estado del proyecto» y en el párrafo del envío: las variables se resuelven con la precedencia entorno → colección → carpetas (de la raíz a la carpeta de la petición), y los datos se leen por ahora de un almacén en memoria.
   - En «Estructura actual», menciona `Adapters/Persistence/InMemoryApiStore`.

9. Al terminar, cambia en este plan `Estado: pendiente de implementar` por `Estado: implementado` y añade una sección `## Resultado`.

## Verificación
Desde la raíz del repo:
1. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64`: sin avisos IL2xxx/IL3xxx.
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`
3. `dotnet test ApiTUI/ApiTUI.slnx`. Para iterar: `--filter "FullyQualifiedName~SendApiRequestUseCaseTests|FullyQualifiedName~InMemoryApiStoreTests|FullyQualifiedName~ApiTuiAppTests|FullyQualifiedName~MessagesTests"`.
4. `dotnet run --project ApiTUI/ApiTUI`: la demo GET sigue devolviendo 200 (es y `--Language en`) y el contenedor valida al arrancar.

## Riesgos / cosas a vigilar
- **Cambio de firma:** `ExecuteAsync` añade `environmentId`. Hay que actualizar todas las llamadas (demo y tests).
- **Precedencia:** se aplica en orden y la última asignación gana. Hay que invertir la cadena de carpetas, que se construye de la hoja a la raíz.
- **Ciclos:** el dominio solo impide que una carpeta sea su propia padre. Los ciclos indirectos se cortan aquí con el `HashSet` de visitadas.
- **Nulabilidad:** `CollectionId`/`ParentFolderId` son anulables; se usan patrones `is { } x` y nunca `!`.
- **Valores de variables:** no se resuelven de forma recursiva. Un valor `{{otra}}` se inserta literal, que es el comportamiento actual de `StringWithVars.Resolve`.
- **DI:** las tres interfaces deben resolver la misma instancia del almacén. Si no, lo que la demo añade no se vería al leer.
- **Concurrencia:** `ConcurrentDictionary` porque el almacén es singleton.

## Fuera de alcance
- Persistencia JSON y lectura desde `data/`; el almacén en memoria es provisional.
- Selección del entorno activo en la UI o por configuración. La demo pasa `null`.
- Variables globales y variables a nivel de petición.
- Resolución recursiva de variables dentro de valores de variables.
- Caché de lecturas o carga de la jerarquía completa.

## Resultado

- El envío combina las variables habilitadas del entorno, la colección y las carpetas. Los tres lectores comparten el almacén en memoria. La demo registra su colección antes de enviar.
- Las referencias ausentes y los ciclos devuelven errores traducibles antes de llamar al transporte.
- Pasan los 351 tests, la comprobación de formato y la compilación Native AOT para Windows x64 sin avisos. La demo devolvió HTTP 200 en español e inglés.

## Ajustes tras la revisión

- La precedencia se alinea con [Bruno](https://docs.usebruno.com/variables/overview): colección < entorno < carpeta raíz < … < carpeta de la petición. El entorno sustituye ahora los valores por defecto de la colección. Antes era al revés. El entorno se sigue leyendo primero, así que `EnvironmentNotFound` mantiene su prioridad, pero sus variables se aplican después de las de la colección.
- `Execute_UsesBrunoPrecedence` cubre el nuevo caso: si la carpeta no define la variable, gana el entorno; y si la variable del entorno está deshabilitada, se usa la de la colección.
- Se corrigen en el README los caracteres acentuados que habían quedado como `?`.
- Verificado: 352 tests superados y formato correcto.
