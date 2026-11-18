# Plan: Entidad de dominio ApiEnvironment (nombre + variables)

Fecha: 2026-11-18
Estado: implementado

## Objetivo
Crear la entidad `ApiEnvironment`: un entorno con identificador, nombre y su lista de variables. Sigue el mismo modelo que `ApiCollection`.

## Contexto
- El README prevé los entornos (`data/environments/<Entorno>.json`) y dice que los marcadores `{{var}}` darán error «hasta que se implementen los entornos».
- **Nombre:** se usa `ApiEnvironment` (decisión del usuario). Un `Environment` en `ApiTUI.Domain` taparía `System.Environment` en los namespaces `ApiTUI.Domain*`. Por ejemplo, `MessagesTests` llama a `Environment.GetEnvironmentVariable` y dejaría de compilar. Además, `ApiEnvironment` es coherente con `ApiCollection`, `ApiFolder` y `ApiRequest`.
- **Identidad:** el entorno lleva `ApiEnvironmentId` e igualdad por Id, como el resto de entidades (decisión del usuario).
- **Dependencia:** este plan se apoya en el trabajo del plan `plans/2026-11-18-variables-coleccion-carpeta.md`, que está implementado pero aún **sin commitear**: `VarValueList`, `ApiCollection.Variables` y los códigos `VarValueList.*`. No hay que deshacer ni mezclar esos cambios. Si se commitea, hazlo en un commit aparte, antes que este.
- Piezas que se reutilizan:
  - `Domain/Identifiers.cs`: `Identifier` base y patrón `Create(id, x => new T(x))`.
  - `Domain/ItemName.cs`: nombre válido como nombre de fichero (`environments/<Entorno>.json`).
  - `Domain/VarValueList.cs`: `Create(IEnumerable<VarValue?>?)` → lista de solo lectura, sin nulos ni nombres repetidos.
  - `Domain/ApiCollection.cs`: plantilla exacta de la entidad (constructor privado, `Create`, igualdad por Id, `==`/`!=`).
  - `tests/ApiTUI.Domain.Tests/ApiCollectionTests.cs`: plantilla de los tests.

## Pasos

1. **`Domain/Identifiers.cs`**: añade
   ```csharp
   public sealed record ApiEnvironmentId : Identifier
   {
       private ApiEnvironmentId(string id) : base(id) { }
       public static Result<ApiEnvironmentId> Create(string? id) => Create(id, x => new ApiEnvironmentId(x));
   }
   ```

2. **`Domain/ApiEnvironment.cs`** (nuevo): `sealed class ApiEnvironment : IEquatable<ApiEnvironment>`.
   - Propiedades: `ApiEnvironmentId Id`, `ItemName Name`, `IReadOnlyList<VarValue> Variables`.
   - `public static Result<ApiEnvironment> Create(ApiEnvironmentId? id, string? name, IEnumerable<VarValue?>? variables = null)`.
   - Orden de validación:
     - `id` null → `IdNull`, código `ApiEnvironment.IdRequired`.
     - Nombre → propaga el `Error` de `ItemName.Create`.
     - Variables → propaga el `Error` de `VarValueList.Create`.
   - Igualdad por `Id` (`Equals`, `GetHashCode`, `==`, `!=`), igual que `ApiCollection`.

3. **Códigos y traducciones**:
   - `DomainErrorCodes`: `ApiEnvironmentIdRequired = "ApiEnvironment.IdRequired"` (sin argumento).
   - `Localization/Messages.resx` (es): «El identificador del entorno es obligatorio.»
   - `Localization/Messages.en.resx` (en): «The environment identifier is required.»

4. **Tests** en `ApiTUI/tests/ApiTUI.Domain.Tests/`:
   - `IdentifiersTests.cs`: añade `ApiEnvironmentId` a los casos existentes (válido con trim; null o vacío → `Identifier.Required` con el nombre del tipo como argumento), siguiendo el patrón de los otros identificadores.
   - `ApiEnvironmentTests.cs` (nuevo):
     - Creación válida: conserva el `Id`, recorta el nombre y deja `Variables` vacía si no se pasan.
     - Con variables: las conserva en orden y son las mismas instancias.
     - `id` null → `IdNull`/`ApiEnvironment.IdRequired`.
     - Nombre no válido (p. ej. `"CON"`) → mismo código y argumentos que `ItemName.Create`.
     - Variable repetida → `VarValueList.Duplicated` con el nombre; un `null` dentro → `VarValueList.ItemNull`.
     - Igualdad: mismo Id con distinto nombre o variables → iguales (también `==` y mismo `GetHashCode`); distinto Id → distintos (`!=`).

5. **README**: en «Estado del proyecto», menciona que el dominio modela también entornos con sus variables. La resolución de variables en el envío sigue pendiente.

6. Al terminar, cambia en este plan `Estado: pendiente de implementar` por `Estado: implementado` y añade una sección `## Resultado`.

## Verificación
Desde la raíz del repo:
1. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64`: sin avisos IL2xxx/IL3xxx.
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`
3. `dotnet test ApiTUI/ApiTUI.slnx`. Para iterar: `--filter "FullyQualifiedName~ApiEnvironmentTests|FullyQualifiedName~IdentifiersTests|FullyQualifiedName~MessagesTests"`.

## Riesgos / cosas a vigilar
- `MessagesTests` exige que `ApiEnvironment.IdRequired` esté en los dos `.resx`. No lleva `{0}`, así que no va en `withArgument`.
- No hay que crear ningún tipo llamado `Environment` en los namespaces del proyecto.
- No se usa `!`: tras comprobar `IsSuccess`, `MemberNotNullWhen` garantiza `Value`/`Error`.
- Sin variables, la lista debe ser vacía y nunca `null`. Esto ya lo garantiza `VarValueList.Create(null)`.

## Fuera de alcance
- Entorno activo, selección de entorno y resolución de `{{var}}` en `SendApiRequestUseCase.CreateVariables()`.
- Qué valor prevalece entre entorno, colección y carpetas.
- Variables secretas o enmascaradas, y edición de entornos (renombrar, añadir o quitar variables).
- Serialización JSON y persistencia en `environments/<Entorno>.json`.

## Resultado

- Añadidos `ApiEnvironmentId` y la entidad `ApiEnvironment`, con nombre `ItemName`, variables validadas mediante `VarValueList` e igualdad por identificador.
- Añadido el código y las traducciones del error por identificador nulo.
- Extendidas las pruebas de identificadores y añadidas pruebas de creación, errores y igualdad de `ApiEnvironment`.
- README actualizado para incluir los entornos; la resolución de variables en el envío sigue pendiente.
- Tests filtrados de `ApiEnvironmentTests`, `IdentifiersTests` y `MessagesTests`: 65 superados.
- `dotnet test ApiTUI/ApiTUI.slnx -m:1 -nr:false -p:UseSharedCompilation=false`: 338 superados.
- `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`: correcto.
- Publicación Native AOT `win-x64`: correcta, sin avisos IL2xxx/IL3xxx.
