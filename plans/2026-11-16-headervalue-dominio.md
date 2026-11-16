# Plan: Clase de dominio HeaderValue

Fecha: 2026-11-16
Estado: implementado

## Objetivo
Crear `HeaderValue`, que representa una cabecera de una petición con:
- `Name`: `HeaderName` obligatorio (no puede ser `null`).
- `Value`: `StringWithVars` cuyo texto puede ser `null` o vacío (puede contener `{{var}}`).
- `Enabled`: `bool` que indica si la cabecera se envía.

## Contexto
- Value objects existentes en `ApiTUI/ApiTUI/Domain/`:
  - `HeaderName.cs`: `sealed record`, `Create(string?)` → `Result<HeaderName>`, igualdad sin distinguir mayúsculas.
  - `StringWithVars.cs`: `sealed partial record StringWithVars(string? Text)` con `Resolve(...)`.
  - `VarName.cs`: patrón `Create` → `Result<T>`.
- `Result<T>`/`ErrorsReasons.InvalidInput` en `ApiTUI/ApiTUI/Common/`.
- Tests xUnit en `ApiTUI/tests/ApiTUI.Domain.Tests` (p. ej. `HeaderNameTests.cs`).
- Reglas `.claude/skills/csharp-skill/SKILL.md`: `sealed`, sin `!`, no declarar anulable lo que el dominio considera obligatorio, compatible con Native AOT.

## Pasos
1. Crear `ApiTUI/ApiTUI/Domain/HeaderValue.cs`, namespace `ApiTUI.Domain`:
   ```csharp
   public sealed record HeaderValue
   {
       public HeaderName Name { get; }
       public StringWithVars Value { get; }
       public bool Enabled { get; init; }

       private HeaderValue(HeaderName name, StringWithVars value, bool enabled)
       {
           Name = name;
           Value = value;
           Enabled = enabled;
       }

       public static Result<HeaderValue> Create(HeaderName? name, StringWithVars? value, bool enabled = true)
       {
           if (name is null)
               return Result<HeaderValue>.Failure(ErrorsReasons.InvalidInput, "El nombre de la cabecera es obligatorio.");

           return Result<HeaderValue>.Success(new HeaderValue(name, value ?? new StringWithVars(null), enabled));
       }
   }
   ```
   - `Name` y `Value` son propiedades no anulables; el «valor nulo o vacío» vive en `Value.Text`. Si llega `value` `null`, se normaliza a `new StringWithVars(null)` para que los consumidores nunca comprueben `Value is null`.
   - `Enabled` por defecto `true` (una cabecera nueva se envía) y con `init`, de modo que activar/desactivar se hace con `header with { Enabled = false }` sin poder romper las demás reglas.
   - `Name` y `Value` sin `init`: así un `with` no puede asignar `Name = null`.
   - Igualdad sintetizada del record: compara `Name` (sin distinguir mayúsculas, por `HeaderName`), `Value` (por `Text`) y `Enabled`.
2. Crear `ApiTUI/tests/ApiTUI.Domain.Tests/HeaderValueTests.cs` con helper `Name(string)` vía `HeaderName.Create` (asertando `IsSuccess`). Casos:
   - Válido: nombre + valor `"application/json"` → `IsSuccess`, propiedades conservadas, `Enabled == true` por defecto.
   - `enabled: false` se conserva.
   - Valor con texto `null`, `""` y con `{{token}}` → válidos, `Value.Text` conservado.
   - `value` `null` → válido y `Value.Text` es `null` (`Value` no es `null`).
   - `name` `null` → falla con `InvalidInput`.
   - `with { Enabled = false }` cambia solo `Enabled`.
   - Igualdad: `Content-Type`/`content-type` con mismo valor y `Enabled` → iguales y mismo hash; distinto `Value` o `Enabled` → distintos.
3. No tocar `HeaderName`, `StringWithVars`, `VarName` ni la estructura de proyectos.
4. Al terminar, cambiar en este plan `Estado: pendiente de implementar` por `Estado: implementado`.

## Verificación
Desde la raíz del repo:
1. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64` (sin avisos IL2xxx/IL3xxx).
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`
3. `dotnet test ApiTUI/ApiTUI.slnx` (y `--filter "FullyQualifiedName~HeaderValueTests"`).

## Riesgos / cosas a vigilar
- Pasar `null` a `Create` en el test de nombre nulo es legal porque el parámetro es `HeaderName?`; no hace falta `!` ni trucos para esquivar la nulabilidad.
- No añadir `init` a `Name`/`Value`: permitiría `with { Name = null }` saltándose la validación.
- El valor de cabecera no se valida contra HTTP (CR/LF, controles) porque puede contener `{{var}}`; esa validación corresponde al valor ya resuelto, al construir la petición.

## Fuera de alcance
- Validar el valor resuelto según HTTP (CR/LF, caracteres de control).
- Colección de cabeceras de una petición, duplicados y orden.
- Cabeceras prohibidas o gestionadas por `HttpClient`.
- Serialización JSON de `HeaderValue`.
