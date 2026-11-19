# Plan: Añadir `VarsOut` a `ApiRequest`

Fecha: 2026-11-19
Estado: pendiente de implementar

## Objetivo

Añadir a la entidad de dominio `ApiRequest` una propiedad `VarsOut` de tipo colección de `VarValue`, que puede ser `null` o una colección (posiblemente vacía).

## Contexto

- `ApiTUI/ApiTUI/Domain/ApiRequest.cs`: entidad con constructor privado y factorías `CreateInCollection` / `CreateInFolder` que delegan en `Create` privado; devuelven `Result<ApiRequest>`.
- `ApiTUI/ApiTUI/Domain/VarValueList.cs`: `VarValueList.Create(IEnumerable<VarValue?>?)` ya valida elementos nulos (`VarValueListItemNull`) y nombres duplicados (`VarValueListDuplicated`) y devuelve `IReadOnlyList<VarValue>` inmutable (copia). Se usa en `ApiFolder` para `Variables`.
- Llamadores actuales (todos usan parámetros por defecto/nombrados, no se rompen si el nuevo parámetro es opcional y va al final): `ApiTuiApp.cs:102`, `ApiRequestTests.cs`, `SendApiRequestUseCaseTests.cs`.

## Pasos

1. `ApiTUI/ApiTUI/Domain/ApiRequest.cs`
   - Añadir `public IReadOnlyList<VarValue>? VarsOut { get; }`.
   - Ampliar el constructor privado con `IReadOnlyList<VarValue>? varsOut`.
   - Añadir parámetro opcional final `IEnumerable<VarValue?>? varsOut = null` en `CreateInCollection` y `CreateInFolder`, y propagarlo a `Create`.
   - En `Create`, tras validar cabeceras:
     - Si `varsOut is null` → `VarsOut = null` (se conserva la nulidad, no se convierte en vacía).
     - Si no → `VarValueList.Create(varsOut)`; si falla, devolver `Result<ApiRequest>.Failure(varList.Error)`; si va bien, usar `varList.Value` (vacía si la entrada estaba vacía).
   - La igualdad sigue basada solo en `Id` (no tocar).
2. `ApiTUI/tests/ApiTUI.Domain.Tests/ApiRequestTests.cs` — añadir tests:
   - Por defecto (`Create_Defaults_ProducesEmptyDraft`): `Assert.Null(result.Value.VarsOut)`.
   - `varsOut: []` → `VarsOut` no nulo y vacío.
   - Lista con varias `VarValue` (en colección y en carpeta) → conserva orden, valores y `Enabled`, y es copia (modificar la lista origen no afecta).
   - Lista expuesta es de solo lectura (`IList.IsReadOnly`, `NotSupportedException`).
   - Elemento `null` → falla con `ErrorsReasons.InvalidInput` y `DomainErrorCodes.VarValueListItemNull`.
   - Nombres duplicados → falla con `DomainErrorCodes.VarValueListDuplicated` y el nombre en `Args`.
   - Helper `Var(string name, string value, bool enabled = true)` usando `VarName.Create` y `VarValue.Create`.
3. No se modifican `ApiTuiApp.cs`, `SendApiRequestUseCase`, recursos de localización (los códigos de error ya existen y están traducidos) ni README.

## Verificación

- `dotnet build ApiTUI/ApiTUI.slnx --configuration Release` sin warnings nuevos.
- `dotnet test ApiTUI/tests/ApiTUI.Domain.Tests --filter "FullyQualifiedName~ApiRequestTests"` y después `dotnet test ApiTUI/ApiTUI.slnx`.
- `dotnet format --verify-no-changes` (regla del skill `csharp-skill`).

## Riesgos / cosas a vigilar

- Mantener la distinción `null` vs vacía: no normalizar `null` a lista vacía.
- Poner el nuevo parámetro al final para no romper llamadas posicionales existentes.
- Compatibilidad AOT: no introduce reflexión.

## Fuera de alcance

- Uso de `VarsOut` en `SendApiRequestUseCase` (extraer variables de la respuesta) y persistencia JSON.
