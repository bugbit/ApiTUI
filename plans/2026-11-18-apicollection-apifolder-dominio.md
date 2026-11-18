# Plan: Entidades de dominio ApiCollection y ApiFolder (con subcarpetas) y value object ItemName

Fecha: 2026-11-18
Estado: implementado

## Objetivo
1. Crear el value object `ItemName`: nombre válido como nombre de fichero/carpeta en todos los SO soportados. Lo usan `ApiRequest`, `ApiCollection` y `ApiFolder`.
2. Crear la entidad `ApiCollection` (Id + nombre).
3. Crear la entidad `ApiFolder`, que admite subcarpetas: está **o bien** en la raíz de una colección (`ApiCollectionId`) **o bien** dentro de otra carpeta (`ApiFolderId` padre). Es exactamente una de las dos, con el mismo patrón que `ApiRequest`.
4. Migrar `ApiRequest` para que use `ItemName`. La regla de nombre deja de estar en `ApiRequest`.

## Contexto
- `ApiTUI/ApiTUI/Domain/Identifiers.cs` ya define `ApiCollectionId` y `ApiFolderId`. `ApiRequest` (`Domain/ApiRequest.cs`) ya los referencia con las factorías `CreateInCollection`/`CreateInFolder` y el invariante colección XOR carpeta. Faltan las entidades a las que apuntan.
- README: «Agrupar peticiones en colecciones y subcarpetas», «Crear y renombrar colecciones».
- `ApiRequest` valida hoy el nombre en línea: no vacío y trim; sin caracteres de control ni `<>:"/\|?*`; sin punto final; sin nombres reservados de Windows (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`, también con extensión). Usa los códigos `ApiRequest.NameEmpty|NameInvalidChars|NameEndsWithDot|NameReserved`. Colección y carpeta se materializarán como carpetas en disco, así que necesitan la misma regla. Hay tres consumidores reales que deben cambiar a la vez, y por eso se extrae a un value object (decisión confirmada por el usuario).
- Patrones a reutilizar:
  - Value object: `Domain/VarName.cs` (`sealed record`, constructor privado, `Create(string?)` → `Result<T>`, `ToString()` → `Value`).
  - Entidad: `Domain/ApiRequest.cs` (`sealed class`, `IEquatable<T>` por `Id`, `==`/`!=`/`GetHashCode`, factorías explícitas por ubicación).
  - Errores: `Common/Result.cs` (`Result<T>.Failure(reason, code, args)`, `Result<T>.Failure(Error)`), `Common/ErrorsReasons.cs` (`InvalidInput`, `IdNull`).
  - Códigos: `Domain/DomainErrorCodes.cs` y traducciones en `Localization/Messages.resx` (es) y `Messages.en.resx` (en).
  - `tests/ApiTUI.Domain.Tests/MessagesTests.cs` exige que los catálogos contengan **exactamente** los códigos de `DomainErrorCodes` y que los códigos con argumento estén en la lista `withArgument`.
- Reglas: `.claude/skills/csharp-skill/SKILL.md` (sealed, sin `!`, sin anulables en lo obligatorio, YAGNI, Native AOT, verificación final).

## Pasos

1. **`ApiTUI/ApiTUI/Domain/ItemName.cs`** (nuevo), namespace `ApiTUI.Domain`:
   ```csharp
   public sealed record ItemName
   {
       public string Value { get; }
       private ItemName(string value) => Value = value;
       public static Result<ItemName> Create(string? value) { ... }
       public override string ToString() => Value;
   }
   ```
   - Mueve aquí, sin cambiar el comportamiento, la validación actual de `ApiRequest` (constantes `InvalidNameChars` y `ReservedNames`, trim y el mismo orden de comprobaciones).
   - Códigos nuevos en `DomainErrorCodes`: `ItemNameEmpty = "ItemName.Empty"`, `ItemNameInvalidChars = "ItemName.InvalidChars"` (arg: caracteres no válidos), `ItemNameEndsWithDot = "ItemName.EndsWithDot"`, `ItemNameReserved = "ItemName.Reserved"` (arg: nombre reservado). Todos con `ErrorsReasons.InvalidInput`.
   - Comentario breve: el nombre se usa como nombre de fichero/carpeta y se aplica el conjunto más restrictivo (Windows).

2. **Migrar `ApiRequest`**:
   - `public ItemName Name { get; }` (antes `string`).
   - Las factorías siguen recibiendo `string? name`, así que `ApiTuiApp.cs` no cambia. Internamente llaman a `ItemName.Create(name)` y, si falla, devuelven `Result<ApiRequest>.Failure(nameResult.Error)`. El orden de validación se mantiene (id → ubicación → nombre → método → cabeceras).
   - Elimina de `ApiRequest` las constantes y la lógica de nombre.
   - Elimina `ApiRequestNameEmpty|NameInvalidChars|NameEndsWithDot|NameReserved` de `DomainErrorCodes` y sus entradas en ambos `.resx`.

3. **`ApiTUI/ApiTUI/Domain/ApiCollection.cs`** (nuevo): `sealed class ApiCollection : IEquatable<ApiCollection>`.
   - Propiedades: `ApiCollectionId Id`, `ItemName Name`.
   - `public static Result<ApiCollection> Create(ApiCollectionId? id, string? name)`:
     - `id` null → `IdNull`, código `ApiCollection.IdRequired`.
     - Nombre inválido → propaga el `Error` de `ItemName`.
   - Igualdad por `Id`, como en `ApiRequest`.

4. **`ApiTUI/ApiTUI/Domain/ApiFolder.cs`** (nuevo): `sealed class ApiFolder : IEquatable<ApiFolder>`.
   - Propiedades: `ApiFolderId Id`, `ItemName Name`, `ApiCollectionId? CollectionId`, `ApiFolderId? ParentFolderId`. Se cumple siempre que exactamente una de las dos ubicaciones tiene valor.
   - Factorías:
     - `CreateInCollection(ApiFolderId? id, ApiCollectionId? collectionId, string? name)`: carpeta en la raíz de la colección.
     - `CreateInFolder(ApiFolderId? id, ApiFolderId? parentFolderId, string? name)`: subcarpeta.
     - Ambas delegan en un `private static Create(id, collectionId, parentFolderId, name)` común.
   - Validación, en orden:
     - `id` null → `IdNull`, código `ApiFolder.IdRequired`.
     - Ubicación null → `IdNull`, código `ApiFolder.LocationRequired`.
     - `parentFolderId == id` → `InvalidInput`, código `ApiFolder.ParentIsSelf`. Es el único ciclo detectable sin ver el árbol; los ciclos indirectos corresponden a la capa de aplicación.
     - Nombre → propaga el error de `ItemName`.
   - Igualdad por `Id`.

5. **Códigos y traducciones**:
   - En `DomainErrorCodes`: `ApiCollectionIdRequired`, `ApiFolderIdRequired`, `ApiFolderLocationRequired`, `ApiFolderParentIsSelf` y los cuatro `ItemName*`.
   - Entradas en `Messages.resx` (es) y `Messages.en.resx` (en). Los textos de `ItemName` son neutros, por ejemplo: «El nombre no puede estar vacío.» / «El nombre no puede contener caracteres de control ni {0}.» / «El nombre no puede terminar en punto.» / «El nombre no puede ser un nombre reservado del sistema ({0}).» (y su versión en inglés).
   - En `MessagesTests.withArgument`, sustituye `ApiRequestNameInvalidChars` y `ApiRequestNameReserved` por `ItemNameInvalidChars` e `ItemNameReserved`.

6. **Tests** en `ApiTUI/tests/ApiTUI.Domain.Tests/`:
   - `ItemNameTests.cs` (nuevo): pasa aquí los casos de nombre de `ApiRequestTests`: vacío/espacios/null, trim, cada carácter inválido y de control, punto final, reservados con distintas mayúsculas y con extensión (`con.txt`), y nombres válidos parecidos (`CONSOLE`, `COM10`).
   - `ApiRequestTests.cs`: `request.Name` → `request.Name.Value`, y códigos `ApiRequestName*` → `ItemName*`. Deja **un** caso por factoría que compruebe que se propaga el error de nombre; quita los duplicados que ya cubre `ItemNameTests`.
   - `ApiCollectionTests.cs` (nuevo): creación válida conserva propiedades, trim del nombre, id null → `IdNull`/`ApiCollection.IdRequired`, nombre inválido → código `ItemName.*`, igualdad por Id (mismo Id con distinto nombre son iguales; distinto Id son distintos; `==`, `!=`, `GetHashCode`).
   - `ApiFolderTests.cs` (nuevo):
     - `CreateInCollection` conserva `CollectionId` y deja `ParentFolderId` null; `CreateInFolder` hace lo contrario.
     - id null y ubicación null → `IdNull` con sus códigos, en ambas factorías.
     - `CreateInFolder(id, id, ...)` → `ApiFolder.ParentIsSelf`.
     - Nombre inválido en ambas factorías → `ItemName.*`.
     - Igualdad por Id.

7. **README** («Estado del proyecto» / descripción de la solución): añade una frase indicando que el dominio ya modela colecciones, carpetas anidadas y peticiones. No toques todavía la estructura de `data/`; se definirá con la persistencia.

8. Al terminar, cambia en este plan `Estado: pendiente de implementar` por `Estado: implementado` y añade una sección `## Resultado`.

## Verificación
Desde la raíz del repo:
1. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64`: sin avisos IL2xxx/IL3xxx.
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`
3. `dotnet test ApiTUI/ApiTUI.slnx`. Para iterar: `--filter "FullyQualifiedName~ItemNameTests|FullyQualifiedName~ApiCollectionTests|FullyQualifiedName~ApiFolderTests|FullyQualifiedName~ApiRequestTests|FullyQualifiedName~MessagesTests"`.
4. `dotnet run --project ApiTUI/ApiTUI`: la prueba GET sigue funcionando (es y `--Language en`).

## Riesgos / cosas a vigilar
- `MessagesTests` falla si sobra o falta un código en cualquiera de los dos `.resx`, o si un placeholder `{0}` no coincide con la lista `withArgument`.
- Al mover la validación no hay que cambiar el comportamiento: mismo trim, mismo orden de comprobaciones y mismos argumentos de error (la cadena `InvalidNameChars` y el `baseName` reservado).
- `ApiRequest.Name` pasa de `string` a `ItemName`. Cualquier uso de `request.Name` como string debe pasar a `.Value`; hoy solo aparece en los tests.
- No se usa `!`: tras comprobar `IsSuccess`, `MemberNotNullWhen` garantiza `Value`/`Error`.
- El invariante «exactamente una ubicación» de `ApiFolder` lo garantizan las factorías; un futuro `Move...` debe mantenerlo.

## Fuera de alcance
- Detectar ciclos indirectos (A → B → A), comprobar que la colección o carpeta padre existe y garantizar nombres únicos entre hermanos: corresponde a la capa de aplicación o persistencia.
- Variables de colección, autenticación o descripción en `ApiCollection`.
- Métodos de edición: renombrar o mover colecciones, carpetas y peticiones.
- Serialización JSON, persistencia y la estructura definitiva de `data/` con carpetas anidadas.
- Vistas de Terminal.Gui (árbol de colecciones).

## Resultado

- Añadidos `ItemName`, `ApiCollection` y `ApiFolder`; `ApiRequest.Name` ahora usa `ItemName`.
- Añadidos códigos y traducciones para los nuevos errores en español e inglés.
- Añadidos tests para validación de nombres, entidades, ubicaciones e igualdad.
- README actualizado con el estado actual del dominio.
- `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`: correcto.
- `dotnet test ApiTUI/ApiTUI.slnx -m:1 -nr:false -p:UseSharedCompilation=false`: 304 tests superados.
- Publicación Native AOT `win-x64`: correcta, sin avisos IL2xxx/IL3xxx.
- Prueba GET a jsonplaceholder: HTTP 200 en español y en inglés. La conexión requirió ejecución fuera del sandbox.
