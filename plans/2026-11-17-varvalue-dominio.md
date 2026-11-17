# Plan: Clase de dominio VarValue

Fecha: 2026-11-17
Estado: implementado

## Objetivo
Crear `VarValue`, equivalente a `HeaderValue` pero para variables:
- `Name`: `VarName` obligatorio (no puede ser `null`).
- `Value`: `StringWithVars` cuyo texto puede ser `null` o vacío (puede contener `{{var}}`).
- `Enabled`: `bool` que indica si la variable está activa.

## Contexto
- Modelo a seguir: `ApiTUI/ApiTUI/Domain/HeaderValue.cs` (commit `019583c`) y sus tests `ApiTUI/tests/ApiTUI.Domain.Tests/HeaderValueTests.cs`.
- `VarName` (`ApiTUI/ApiTUI/Domain/VarName.cs`): `Create(string?)` → `Result<VarName>`; igualdad por valor **sensible a mayúsculas** (a diferencia de `HeaderName`).
- `StringWithVars`, `Result<T>`, `ErrorsReasons.InvalidInput` ya existen.
- Reglas `.claude/skills/csharp-skill/SKILL.md`: `sealed`, sin `!`, sin anulables en lo obligatorio, Native AOT; DRY solo cuando los consumidores cambian a la vez → `HeaderValue` y `VarValue` son conceptos distintos que pueden evolucionar por separado, así que **no** se extrae una base genérica común.

## Pasos
1. Crear `ApiTUI/ApiTUI/Domain/VarValue.cs`, namespace `ApiTUI.Domain`, con la misma forma que `HeaderValue`:
   ```csharp
   public sealed record VarValue
   {
       public VarName Name { get; }
       public StringWithVars Value { get; }
       public bool Enabled { get; init; }

       private VarValue(VarName name, StringWithVars value, bool enabled)
       {
           Name = name;
           Value = value;
           Enabled = enabled;
       }

       public static Result<VarValue> Create(VarName? name, StringWithVars? value, bool enabled = true)
       {
           if (name is null)
               return Result<VarValue>.Failure(ErrorsReasons.InvalidInput, "El nombre de la variable es obligatorio.");

           return Result<VarValue>.Success(new VarValue(name, value ?? new StringWithVars(null), enabled));
       }
   }
   ```
   - `Name`/`Value` sin `init` (un `with` no puede dejar `Name = null`); `Enabled` con `init` y `true` por defecto.
   - `value` `null` se normaliza a `new StringWithVars(null)`.
2. Crear `ApiTUI/tests/ApiTUI.Domain.Tests/VarValueTests.cs` replicando los casos de `HeaderValueTests` con helper `Name(string)` vía `VarName.Create`:
   - Nombre + valor → propiedades conservadas (`Assert.Same`), `Enabled == true` por defecto.
   - `enabled: false` se conserva.
   - Texto `null`, `""`, `{{token}}` → válidos, texto conservado.
   - `value` `null` → `Value` no nulo con `Text == null`.
   - `name` `null` (con `enabled` true/false) → `InvalidInput`.
   - `with { Enabled = false }` cambia solo `Enabled` y no altera el original.
   - Igualdad: mismo nombre, valor y `Enabled` → iguales y mismo hash.
   - Desigualdad: distinto nombre, distinto valor, distinto `Enabled` y **distinto caso en el nombre** (`baseUrl` vs `BaseUrl`), porque `VarName` es sensible a mayúsculas.
3. No tocar `HeaderValue`, `VarName`, `StringWithVars` ni la estructura de proyectos.
4. Al terminar, cambiar en este plan `Estado: pendiente de implementar` por `Estado: implementado`.

## Verificación
Desde la raíz del repo:
1. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64` (sin avisos IL2xxx/IL3xxx).
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`
3. `dotnet test ApiTUI/ApiTUI.slnx` (y `--filter "FullyQualifiedName~VarValueTests"`).

## Riesgos / cosas a vigilar
- No copiar el test de igualdad «ignorando mayúsculas» de `HeaderValueTests`: en `VarValue` el nombre distingue mayúsculas.
- Duplicación deliberada con `HeaderValue`; si en el futuro ambos deben cambiar siempre a la vez, valorar entonces una abstracción común.
- Un valor puede referenciar otras variables (`{{otra}}`); la resolución en cadena no se trata aquí.

## Fuera de alcance
- Colecciones de variables (entornos), duplicados y orden.
- Resolución de variables que referencian otras variables.
- Serialización JSON de `VarValue`.
