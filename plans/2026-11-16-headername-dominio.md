# Plan: Value object de dominio HeaderName

Fecha: 2026-11-16
Estado: implementado

## Objetivo
Crear el value object `HeaderName` que representa el nombre de una cabecera HTTP y aplica las reglas de HTTP (RFC 9110 §5.1 y §5.6.2):
- `field-name = token = 1*tchar` → no nulo ni vacío (al menos un carácter).
- `tchar = "!" / "#" / "$" / "%" / "&" / "'" / "*" / "+" / "-" / "." / "^" / "_" / "`" / "|" / "~" / DIGIT / ALPHA` (solo ASCII visible; nada de espacios, controles, delimitadores `"(),/:;<=>?@[\]{}` ni caracteres no ASCII).
- Los nombres de cabecera **no distinguen mayúsculas/minúsculas**: `Content-Type` == `content-type`, pero se conserva el texto original tal como lo escribió el usuario.

## Contexto
- Mismo patrón que `VarName` (`ApiTUI/ApiTUI/Domain/VarName.cs`): `sealed record`, constructor privado, `static Result<VarName> Create(string?)`, errores con `ErrorsReasons.InvalidInput`, caracteres no válidos listados con `Where(...).Distinct()` y `ToString()` devuelve el valor.
- `Result<T>`/`ErrorsReasons` en `ApiTUI/ApiTUI/Common/`.
- Tests en `ApiTUI/tests/ApiTUI.Domain.Tests` (xUnit), p. ej. `VarNameTests.cs`.
- Reglas `.claude/skills/csharp-skill/SKILL.md`: `sealed`, sin `!`, compatible con Native AOT.

## Pasos
1. Crear `ApiTUI/ApiTUI/Domain/HeaderName.cs`, namespace `ApiTUI.Domain`:
   ```csharp
   public sealed record HeaderName
   {
       private const string ExtraTokenChars = "!#$%&'*+-.^_`|~";

       public string Value { get; }
       private HeaderName(string value) => Value = value;

       public static Result<HeaderName> Create(string? value)
       {
           if (string.IsNullOrEmpty(value))
               return Result<HeaderName>.Failure(ErrorsReasons.InvalidInput, "El nombre de la cabecera no puede estar vacío.");

           var invalidChars = value.Where(c => !IsTokenChar(c)).Distinct().ToArray();
           if (invalidChars.Length > 0)
               return Result<HeaderName>.Failure(ErrorsReasons.InvalidInput,
                   $"El nombre de la cabecera contiene caracteres no permitidos: {Describe(invalidChars)}. Solo se admiten letras ASCII, dígitos y !#$%&'*+-.^_`|~.");

           return Result<HeaderName>.Success(new HeaderName(value));
       }

       private static bool IsTokenChar(char c) => char.IsAsciiLetterOrDigit(c) || ExtraTokenChars.Contains(c);

       public bool Equals(HeaderName? other) =>
           other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

       public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

       public override string ToString() => Value;
   }
   ```
   - `Describe` formatea cada carácter como `'x'`, pero los caracteres de control/espacio se muestran legibles (p. ej. `' '`, `'\t'`, o `U+000A`) para que el mensaje no quede roto.
   - Sin `Trim`: `" Accept"` o `"Accept "` son inválidos (el espacio no es `tchar`; RFC 9112 §5.1 prohíbe espacio antes de `:`).
   - Igualdad y hash insensibles a mayúsculas (ordinal, no dependiente de cultura) para poder usar `HeaderName` como clave de diccionario/set sin duplicar `Accept`/`accept`.
2. Crear `ApiTUI/tests/ApiTUI.Domain.Tests/HeaderNameTests.cs`:
   - Válidos (`[Theory]`): `"Accept"`, `"Content-Type"`, `"X-Request-ID"`, `"x_custom.header"`, `"a"`, `"123"`, y uno con todos los `tchar` especiales `"!#$%&'*+-.^_`|~"` → `IsSuccess`, valor preservado tal cual.
   - Vacíos: `null`, `""` → `InvalidInput`.
   - Inválidos: `" "`, `"Content Type"`, `" Accept"`, `"Accept "`, `"Accept:"`, `":authority"`, `"a\tb"`, `"a\r\nb"`, `"a\0b"`, `"a\u007Fb"` (DEL), cada delimitador `"(),/;<=>?@[\]{}` y `"`, no ASCII `"Cabecera-ñ"`, `"Größe"` → `InvalidInput`.
   - Mensaje lista cada carácter inválido una vez: `"a@b@c/"` → contiene `'@', '/'`.
   - Igualdad: `Content-Type` == `content-type` == `CONTENT-TYPE`, mismo `GetHashCode`; un `HashSet<HeaderName>` con ambas formas tiene 1 elemento; `ToString()` conserva el texto original; `Accept` != `Accept-Encoding`.
3. No tocar `VarName`, `StringWithVars` ni la estructura de proyectos.

## Verificación
Desde la raíz del repo:
1. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64` (sin avisos IL2xxx/IL3xxx).
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`
3. `dotnet test ApiTUI/ApiTUI.slnx` (y `--filter "FullyQualifiedName~HeaderNameTests"`).

## Riesgos / cosas a vigilar
- En un `record`, `Equals(HeaderName?)` y `GetHashCode()` personalizados sustituyen a los sintetizados; deben ser coherentes entre sí (ambos `OrdinalIgnoreCase`).
- Los marcadores `{{var}}` no son válidos en un nombre de cabecera (`{` y `}` no son `tchar`); si más adelante se quieren variables en nombres de cabecera, habrá que validar después de resolverlas.
- Caracteres de control en el mensaje de error: formatearlos para que no rompan la UI.

## Fuera de alcance
- Validación de valores de cabecera (`HeaderValue`).
- Cabeceras prohibidas o gestionadas por `HttpClient` (`Host`, `Content-Length`, `Transfer-Encoding`…), pseudo-cabeceras HTTP/2 (`:authority`) y longitud máxima.
- Normalizar mayúsculas/minúsculas del texto guardado.
- Serialización JSON de `HeaderName`.
