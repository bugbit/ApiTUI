# Plan: Value object de dominio VarName

Fecha: 2026-11-16
Estado: implementado; verificaciones pendientes

## Objetivo
Crear el value object `VarName` que representa el nombre de una variable (de entorno/colección). Un `VarName` válido:
- no es nulo, vacío ni solo espacios;
- solo contiene caracteres alfanuméricos ASCII (`a-z`, `A-Z`, `0-9`) y `-`, `_`, `.`.

## Contexto
- Aún no existe el proyecto `ApiTUI.Domain`; el código compartido vive en el proyecto de consola: `ApiTUI/ApiTUI/Common/Result.cs` (`Result<T>`, `Error`) y `ApiTUI/ApiTUI/Common/ErrorsReasons.cs` (`InvalidInput`).
- El proyecto de tests `ApiTUI/tests/ApiTUI.Domain.Tests` (xUnit, `using Xunit` global) ya referencia `ApiTUI/ApiTUI/ApiTUI.csproj`.
- Reglas en `.claude/skills/csharp-skill/SKILL.md`: clases `sealed`, nada de `!`, sin reflexión (Native AOT), verificación final con publish AOT + `dotnet format` + `dotnet test`.
- Decisión: «alfanumérico» = ASCII (`char.IsAsciiLetterOrDigit`), no letras Unicode (á, ñ…), porque son nombres usados en plantillas `{{var}}` y ficheros JSON.

## Pasos
1. Crear `ApiTUI/ApiTUI/Domain/VarName.cs`, namespace `ApiTUI.Domain`:
   ```csharp
   public sealed record VarName
   {
       public string Value { get; }
       private VarName(string value) => Value = value;

       public static Result<VarName> Create(string? value)
       {
           if (string.IsNullOrWhiteSpace(value))
               return Result<VarName>.Failure(ErrorsReasons.InvalidInput, "El nombre de la variable no puede estar vacío.");
           foreach (var c in value)
               if (!IsAllowed(c))
                   return Result<VarName>.Failure(ErrorsReasons.InvalidInput,
                       $"El nombre de la variable contiene el carácter no permitido '{c}'. Solo se admiten letras, dígitos, '-', '_' y '.'.");
           return Result<VarName>.Success(new VarName(value));
       }

       private static bool IsAllowed(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.';

       public override string ToString() => Value;
   }
   ```
   - Constructor privado: la única vía de creación es `Create`, que nunca lanza excepciones (devuelve `Result`).
   - Sin trimming implícito: `" a"` es inválido (el espacio no está permitido).
   - `record` da igualdad por valor (case-sensitive, ordinal).
2. Crear `ApiTUI/tests/ApiTUI.Domain.Tests/VarNameTests.cs` (namespace `ApiTUI.Domain.Tests`) con:
   - `[Theory]` válidos: `"a"`, `"baseUrl"`, `"api-key"`, `"user_id"`, `"env.prod"`, `"A1-b_2.c"`, `"123"` → `IsSuccess`, `Value.Value` igual a la entrada.
   - `[Theory]` vacíos: `null`, `""`, `" "`, `"\t"` → falla con `Reason == ErrorsReasons.InvalidInput`.
   - `[Theory]` caracteres inválidos: `"my var"`, `"a/b"`, `"a{b"`, `"ñandu"`, `"café"`, `"a$"`, `"a@b"` → falla con `InvalidInput`.
   - `[Fact]` igualdad: dos `VarName` creados con el mismo texto son iguales; `ToString()` devuelve el valor.
3. No tocar `UnitTest1.cs` ni la estructura de proyectos.

## Verificación
Desde la raíz del repo:
1. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64` (sin avisos IL2xxx/IL3xxx).
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`
3. `dotnet test ApiTUI/ApiTUI.slnx` (y `--filter "FullyQualifiedName~VarNameTests"`).

## Riesgos / cosas a vigilar
- Ubicación temporal en el proyecto de consola (`ApiTUI/ApiTUI/Domain`) hasta que exista `src/ApiTUI.Domain`; el namespace `ApiTUI.Domain` facilita moverlo después.
- Interpretación de «alfanumérico» como ASCII: si se quisiera Unicode, cambiar a `char.IsLetterOrDigit`.
- No usar `!` ni anulables en `Value`; tras `IsSuccess` el compilador ya garantiza `Value` no nulo.
- Ficheros con la misma codificación/BOM que el resto para que `dotnet format` no se queje.

## Fuera de alcance
- Crear el proyecto `ApiTUI.Domain` o mover `Common`.
- Longitud máxima del nombre, normalización de mayúsculas, serialización JSON de `VarName`.
- Entidades que usen `VarName` (entornos, variables).
