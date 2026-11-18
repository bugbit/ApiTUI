# Plan: Variables en ApiCollection y ApiFolder

Fecha: 2026-11-18
Estado: implementado

## Objetivo
Que `ApiCollection` y `ApiFolder` tengan su propia lista de variables (`IReadOnlyList<VarValue> Variables`), validada en el dominio con una única regla compartida: sin elementos nulos y sin nombres repetidos.

## Contexto
- El commit `77159f8` creó `ApiCollection`, `ApiFolder` e `ItemName`, y dejó las variables fuera de alcance. El usuario pide ahora variables en la colección **y** en la carpeta.
- Tipos que se reutilizan:
  - `Domain/VarName.cs`: `sealed record` con igualdad por `Value`, ordinal y sensible a mayúsculas. Es la misma clave que usa `StringWithVars.Resolve(IReadOnlyDictionary<VarName, object?>)`.
  - `Domain/VarValue.cs`: `Name` (`VarName`), `Value` (`StringWithVars`) y `Enabled`.
- Patrón que se copia: `ApiRequest.Headers` en `Domain/ApiRequest.cs`. Un `IEnumerable<HeaderValue?>?` en la entrada, `null` → lista vacía, un elemento `null` → `InvalidInput`, copia protegida con `Array.AsReadOnly(list.ToArray())` y orden conservado.
- Diferencia con las cabeceras: las variables **no admiten nombres repetidos**, porque `{{var}}` quedaría ambiguo. Las cabeceras sí los admiten.
- Las dos entidades aplican la misma regla y deben cambiar a la vez. Por eso la validación se centraliza en un único sitio (regla DRY de `.claude/skills/csharp-skill/SKILL.md`, con dos consumidores reales).
- `SendApiRequestUseCase.CreateVariables()` sigue devolviendo un diccionario vacío. Resolver las variables queda fuera de esta tarea.
- `tests/ApiTUI.Domain.Tests/MessagesTests.cs` exige que los `.resx` contengan exactamente los códigos de `DomainErrorCodes`, y que los códigos con `{0}` estén en `withArgument`.

## Pasos

1. **`ApiTUI/ApiTUI/Domain/VarValueList.cs`** (nuevo), namespace `ApiTUI.Domain`:
   ```csharp
   public static class VarValueList
   {
       public static Result<IReadOnlyList<VarValue>> Create(IEnumerable<VarValue?>? variables) { ... }
   }
   ```
   - `null` → `Success` con una lista vacía.
   - Algún elemento `null` → `InvalidInput`, código `VarValueList.ItemNull`.
   - Nombre repetido (por igualdad de `VarName`, esté habilitada o no) → `InvalidInput`, código `VarValueList.Duplicated`, con el nombre (`name.Value`) como argumento. Se informa del primer repetido que se encuentre. Para detectarlo se usa un `HashSet<VarName>`.
   - Éxito → `Array.AsReadOnly(validated.ToArray())`, que conserva el orden.
   - Comentario breve: a diferencia de las cabeceras, se rechazan los repetidos para que `{{var}}` tenga un único valor.

2. **`Domain/ApiCollection.cs`**:
   - Nueva propiedad `public IReadOnlyList<VarValue> Variables { get; }`, que se asigna en el constructor privado.
   - Firma: `Create(ApiCollectionId? id, string? name, IEnumerable<VarValue?>? variables = null)`. Las llamadas existentes siguen compilando.
   - Orden de validación: id → nombre → variables. Un error de `VarValueList` se propaga con `Result<ApiCollection>.Failure(result.Error)`.

3. **`Domain/ApiFolder.cs`**:
   - Misma propiedad `Variables`.
   - `CreateInCollection(id, collectionId, name, IEnumerable<VarValue?>? variables = null)` y `CreateInFolder(id, parentFolderId, name, IEnumerable<VarValue?>? variables = null)`. Ambas lo pasan al `Create` privado común.
   - Orden de validación: id → ubicación → carpeta que es su propia padre → nombre → variables.

4. **Códigos y traducciones**:
   - `DomainErrorCodes`: `VarValueListItemNull = "VarValueList.ItemNull"` y `VarValueListDuplicated = "VarValueList.Duplicated"`.
   - `Localization/Messages.resx` (es): «La lista de variables no puede contener elementos nulos.» / «La variable {0} está repetida.»
   - `Messages.en.resx` (en): «The variable list cannot contain null entries.» / «The variable {0} is duplicated.»
   - `MessagesTests.withArgument`: añade `DomainErrorCodes.VarValueListDuplicated`.

5. **Tests** en `ApiTUI/tests/ApiTUI.Domain.Tests/`:
   - `VarValueListTests.cs` (nuevo):
     - `null` → vacía.
     - Vacía → vacía.
     - Conserva el orden y las instancias (`Assert.Same`).
     - Un `null` dentro → `VarValueList.ItemNull`.
     - Repetido → `VarValueList.Duplicated` con el nombre como único argumento, también cuando una de las dos está deshabilitada.
     - Nombres que solo difieren en mayúsculas (`token`/`Token`) se aceptan.
     - Modificar la lista original después de crear no cambia el resultado.
     - El resultado no se puede modificar con un cast a `IList<VarValue>`: `IsReadOnly` es true o la escritura lanza `NotSupportedException`.
   - `ApiCollectionTests.cs`:
     - Sin variables → `Variables` vacía.
     - Con variables → las conserva en orden.
     - Con una variable repetida → propaga `VarValueList.Duplicated`.
   - `ApiFolderTests.cs`:
     - Lo mismo, en **ambas** factorías (`[Theory]` con `inFolder`).
     - Un `null` dentro → propaga `VarValueList.ItemNull`.
   - Helper local en los tests para crear `VarValue` con `VarName.Create(...)`, comprobando `IsSuccess` y sin usar `!`.

6. **README** («Estado del proyecto»): añade que colecciones y carpetas guardan sus propias variables, y que la resolución en el envío sigue pendiente.

7. Al terminar, cambia en este plan `Estado: pendiente de implementar` por `Estado: implementado` y añade una sección `## Resultado`.

## Verificación
Desde la raíz del repo:
1. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64`: sin avisos IL2xxx/IL3xxx.
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`
3. `dotnet test ApiTUI/ApiTUI.slnx`. Para iterar: `--filter "FullyQualifiedName~VarValueListTests|FullyQualifiedName~ApiCollectionTests|FullyQualifiedName~ApiFolderTests|FullyQualifiedName~MessagesTests"`.

## Riesgos / cosas a vigilar
- `MessagesTests` falla si falta un código en algún `.resx`, o si `VarValueList.Duplicated` no está en `withArgument`.
- `IEnumerable<VarValue?>?` permite pasar un `null` dentro de la lista en los tests sin usar `!`.
- Hay que enumerar la entrada una sola vez (copiar mientras se valida), para no evaluar dos veces un `IEnumerable` perezoso.
- Los repetidos se comparan con la igualdad de `VarName`, que distingue mayúsculas. Es la misma igualdad con la que `StringWithVars` resuelve `{{var}}`, así que ambas reglas son coherentes.
- Las firmas de las factorías cambian solo con un parámetro opcional al final, así que las llamadas existentes no cambian.

## Fuera de alcance
- Resolver las variables en `SendApiRequestUseCase` y decidir qué valor prevalece (colección → carpetas → entorno).
- Entornos (`environments/<Entorno>.json`) y variables globales.
- Métodos de edición de variables (añadir, quitar, renombrar).
- Variables en `ApiRequest`.
- Serialización JSON y persistencia.

## Resultado

- Añadido `VarValueList` para validar listas de variables sin elementos nulos ni nombres duplicados.
- `ApiCollection` y `ApiFolder` ahora exponen listas copiadas de solo lectura; las factorías existentes aceptan variables mediante un parámetro opcional.
- Añadidos códigos de error, traducciones y pruebas para listas y ambas entidades.
- README actualizado: el dominio guarda variables, pero la resolución durante el envío sigue pendiente.
- `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`: correcto.
- Tests filtrados de variables, colecciones, carpetas y mensajes: 64 superados.
- `dotnet test ApiTUI/ApiTUI.slnx -m:1 -nr:false -p:UseSharedCompilation=false`: 323 superados.
- Publicación Native AOT `win-x64`: correcta, sin avisos IL2xxx/IL3xxx.
