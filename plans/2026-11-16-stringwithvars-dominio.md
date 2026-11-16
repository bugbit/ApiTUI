# Plan: Value object de dominio StringWithVars

Fecha: 2026-11-16
Estado: implementado

## Objetivo
Crear `StringWithVars`, un texto (que puede ser `null` o vacío) con marcadores `{{var}}`, y un método que devuelve el texto con todas las variables reemplazadas por sus valores a partir de un diccionario `VarName → object?`.

## Contexto
- Ya existe `VarName` (`ApiTUI/ApiTUI/Domain/VarName.cs`): `sealed record`, `Create(string?)` → `Result<VarName>`, caracteres válidos ASCII letras/dígitos y `-`, `_`, `.`; igualdad por valor case-sensitive (sirve como clave de diccionario).
- `Result<T>`/`ErrorsReasons` en `ApiTUI/ApiTUI/Common/`.
- Tests en `ApiTUI/tests/ApiTUI.Domain.Tests` (xUnit), p. ej. `VarNameTests.cs`.
- Reglas `.claude/skills/csharp-skill/SKILL.md`: `sealed`, sin `!`, Native AOT (nada de regex/reflexión en tiempo de ejecución no compatible → usar `[GeneratedRegex]`).
- Decisión del usuario: si una variable del texto no está en el diccionario, el marcador `{{var}}` se deja tal cual (comportamiento tipo Postman).

## Pasos
1. Crear `ApiTUI/ApiTUI/Domain/StringWithVars.cs`, namespace `ApiTUI.Domain`:
   ```csharp
   public sealed partial record StringWithVars(string? Text)
   {
       [GeneratedRegex(@"\{\{([A-Za-z0-9._-]+)\}\}")]
       private static partial Regex VarPattern();

       public string? Resolve(IReadOnlyDictionary<VarName, object?> variables)
       {
           ArgumentNullException.ThrowIfNull(variables);
           if (string.IsNullOrEmpty(Text))
               return Text;                       // null → null, "" → ""

           return VarPattern().Replace(Text, match =>
           {
               var name = VarName.Create(match.Groups[1].Value);
               return name.IsSuccess && variables.TryGetValue(name.Value, out var value)
                   ? Format(value)
                   : match.Value;                 // variable ausente: se deja {{var}}
           });
       }

       private static string Format(object? value) => value switch
       {
           null => string.Empty,
           IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
           _ => value.ToString() ?? string.Empty,
       };

       public override string? ToString() => Text;   // si da problemas de nulabilidad, omitir este override
   }
   ```
   - El patrón del regex reutiliza el mismo conjunto de caracteres que `VarName`, así que un nombre capturado siempre es válido; aun así se comprueba `IsSuccess` (sin `!`).
   - `{{ var }}` (con espacios) o `{{a b}}` no se consideran variables y se dejan intactos.
   - Reemplazo en una sola pasada, no recursivo: si un valor contiene `{{otra}}`, no se vuelve a sustituir.
   - Formato de valores con `CultureInfo.InvariantCulture` (`1.5` y no `1,5`), `null` → cadena vacía.
2. Crear `ApiTUI/tests/ApiTUI.Domain.Tests/StringWithVarsTests.cs` con un helper `Vars(params (string name, object? value)[])` que construye el diccionario vía `VarName.Create` (asertando `IsSuccess`). Casos:
   - `Text` `null` → `Resolve` devuelve `null`; `""` → `""`.
   - Texto sin marcadores → igual al original.
   - Una variable: `"{{host}}/api"` con `host=localhost` → `"localhost/api"`.
   - Varias y repetidas: `"{{a}}-{{b}}-{{a}}"`.
   - Variable ausente → se conserva `{{missing}}`; mezcla presente + ausente.
   - Valor `null` → `""`.
   - Valores no string: `int` 42, `double` 1.5 → `"1.5"` (invariante), `bool`, `Guid`.
   - Nombres con `-`, `_`, `.`: `{{api-key}}`, `{{user_id}}`, `{{env.prod}}`.
   - No variables: `"{{ a }}"`, `"{{}}"`, `"{a}"`, `"{{a b}}"` se dejan intactos.
   - No recursivo: `a = "{{b}}"`, `b = "x"` → resultado `"{{b}}"`.
   - Case-sensitive: `{{Host}}` con clave `host` → se conserva.
   - Diccionario `null` → `ArgumentNullException`.
3. No tocar `VarName` ni la estructura de proyectos.

## Verificación
Desde la raíz del repo:
1. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64` (sin avisos IL2xxx/IL3xxx; el regex generado es compatible con AOT).
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`
3. `dotnet test ApiTUI/ApiTUI.slnx` (y `--filter "FullyQualifiedName~StringWithVarsTests"`).

## Riesgos / cosas a vigilar
- `[GeneratedRegex]` exige `partial` en el record y método `static partial`; no usar `new Regex(...)` en tiempo de ejecución.
- El patrón del regex duplica la regla de caracteres de `VarName`: si cambia una, debe cambiar la otra (los tests de nombres con `-`, `_`, `.` lo vigilan).
- `bool.ToString()` produce `"True"`/`"False"`; se acepta así por ahora (se documenta en los tests).
- El override de `ToString()` con retorno `string?` puede generar aviso de nulabilidad; si lo hace, eliminar el override en vez de usar `!` o suprimir avisos.

## Fuera de alcance
- Variables anidadas/recursivas, espacios dentro de `{{ }}`, escapado de llaves.
- Funciones dinámicas tipo `{{$guid}}`, `{{$timestamp}}`.
- Serialización JSON de `StringWithVars` y su uso en peticiones/entornos.
