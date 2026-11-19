# Plan: VarsOut con variables de la petición y marcadores mal formados

Fecha: 2026-11-19
Estado: pendiente de implementar

## Objetivo

Ajustar la extracción de VarsOut ya implementada (plan `plans/2026-11-19-extraer-varsout-respuesta.md`, cambios aún sin commitear) en dos puntos:

1. **Variables de la petición:** las plantillas de VarsOut pueden mezclar marcadores de la respuesta con variables normales.
   - Ejemplo: `{{response.json.a.v}}_{{var10}}` con la respuesta `{ "a": { "v": "x" } }` y la variable `var10 = "y"` da `x_y`.
2. **Marcadores mal formados:** un marcador `{{...}}` cuyo contenido no es un `VarName` válido (por ejemplo `{{response.json.my prop}}` o `{{ response }}`) devuelve error `VarOutPlaceholderNotSupported`. Hoy se guarda literalmente sin avisar.

## Contexto

- `ApiTUI/ApiTUI/Application/SendApiRequest/ResponseVarExtractor.cs`:
  - Hoy, cualquier marcador que no sea `response` ni `response.json.<ruta>` da error `VarOutPlaceholderNotSupported`.
  - Usa `StringWithVars.GetVarNames()`, que ignora en silencio los `{{...}}` cuyo contenido no es un `VarName` válido.
- `SendApiRequestUseCase.ExecuteAsync` ya tiene el diccionario `variables` (`IReadOnlyDictionary<VarName, object?>`) cargado con la precedencia colección < entorno < carpetas, solo con variables `Enabled`. Es el mismo que se usa para resolver la URL, las cabeceras y el cuerpo.
- `StringWithVars` (`Domain/StringWithVars.cs`) tiene el regex `VarPattern()` privado (`\{\{([^{}]+)\}\}`).
- `ApplicationErrorCodes.UnresolvedVariables` ya existe y está traducido; sus argumentos son los nombres separados por `", "`.

## Pasos

1. **`Domain/StringWithVars.cs`**
   - Añadir `IReadOnlyList<string> GetInvalidPlaceholders()`.
   - Devuelve, sin repetidos y en orden, el contenido de cada `{{...}}` cuyo `VarName.Create` falla. Reutiliza `VarPattern()`.
   - No cambiar `GetVarNames` ni `Resolve`.

2. **`Application/SendApiRequest/ResponseVarExtractor.cs`**
   - Nueva firma: `Evaluate(StringWithVars template, string body, Lazy<JsonDocument?> json, IReadOnlyDictionary<VarName, object?> variables)`.
   - Primero, si `template.GetInvalidPlaceholders()` no está vacío, se devuelve `VarOutPlaceholderNotSupported` con el primero como argumento.
   - Después, para cada nombre de `GetVarNames()`:
     - `response` → el cuerpo de la respuesta.
     - Empieza por `response.json.` con ruta no vacía → valor JSON, con las mismas reglas que ahora.
     - Exactamente `response.json` o `response.json.` → `VarOutPlaceholderNotSupported`, como ahora.
     - Cualquier otro nombre:
       - Si está en `variables`, se usa su valor.
       - Si no, se acumula como no resuelto.
   - Si hay no resueltos, se devuelve `Error(InvalidInput, ApplicationErrorCodes.UnresolvedVariables, string.Join(", ", nombres))`.
   - **Precedencia:** los marcadores `response` y `response.json.*` se interpretan siempre como respuesta, aunque exista una variable con ese nombre.
   - La sustitución sigue haciéndose en una sola pasada con `StringWithVars.Resolve`. El valor de una variable que contenga `{{x}}` no se vuelve a expandir.

3. **`SendApiRequestUseCase`**
   - Pasar a `ApplyVarsOutAsync` el diccionario `variables` ya cargado y reenviarlo a `ResponseVarExtractor.Evaluate`.
   - No se recarga nada: se usan los valores vigentes al enviar la petición.

4. **Tests**
   - `StringWithVarsTests`: `GetInvalidPlaceholders`:
     - Con espacios.
     - Con caracteres no permitidos.
     - Repetidos.
     - Sin inválidos → lista vacía.
     - Mezcla de válidos e inválidos.
   - `ResponseVarExtractorTests`:
     - Actualizar las llamadas existentes con el nuevo parámetro.
     - `{{response.json.a.v}}_{{var10}}` → `x_y`.
     - Solo `{{var10}}` → valor de la variable, sin tocar el JSON (`json.IsValueCreated == false`).
     - Variable con valor null → cadena vacía.
     - Variable inexistente → `UnresolvedVariables` con su nombre.
     - Varias inexistentes → todas en el argumento.
     - Una variable llamada `response` → se usa la respuesta, no la variable.
     - `{{response.json.my prop}}` y `{{ response }}` → `VarOutPlaceholderNotSupported` con el contenido como argumento.
     - Sustituir el caso actual `{{token}}` → `VarOutPlaceholderNotSupported`, porque ahora `{{token}}` se resuelve como variable o da `UnresolvedVariables`.
   - `SendApiRequestUseCaseTests`: un caso de extremo a extremo con `{{response.json.token}}_{{suffix}}`. `suffix` está en el entorno, el destino en la colección, y se comprueba el valor guardado.

## Verificación

- `dotnet build ApiTUI/ApiTUI.slnx --configuration Release` sin advertencias.
- `dotnet test ApiTUI/ApiTUI.slnx`.
- `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`.
- Publicación Native AOT para win-x64 sin advertencias IL2xxx/IL3xxx.

## Riesgos / cosas a vigilar

- No alterar el comportamiento de envío de la URL, las cabeceras y el cuerpo. `GetInvalidPlaceholders` solo se usa en VarsOut.
- Mantener el `JsonDocument` perezoso: solo se analiza el JSON si la plantilla tiene un marcador `response.json.*`.
- Una variable con nombre `response` o `response.json.*` queda oculta en VarsOut. Se documenta con un test.

## Fuera de alcance

- Validar los marcadores mal formados en la URL, las cabeceras o el cuerpo de la petición.
- Resolver variables de forma recursiva.
- El commit. Se hará después de revisar.
