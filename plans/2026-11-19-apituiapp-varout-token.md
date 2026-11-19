# Plan: Demostrar VarsOut en ApiTuiApp con la variable token

Fecha: 2026-11-19
Estado: pendiente de implementar

## Objetivo
En la prueba provisional por consola (`ApiTuiApp`):
1. Crear un entorno con la variable `token`, inicialmente vacía (`""`).
2. Añadir a la petición de login un VarOut `token = {{response.json.token}}`.
3. Enviar la petición usando ese entorno.
4. Al final, mostrar el valor de `token` leído del entorno guardado, que debe ser el token devuelto por la TodoApi.

## Contexto
- `ApiTUI/ApiTUI/ApiTuiApp.cs`:
  - `CreateSampleRequest()` crea una colección `Demo` con la variable `url`, la guarda en `InMemoryApiStore` y crea la petición de login.
  - `RunAsync` llama a `ExecuteAsync(request, null, ct)`, sin entorno.
- La TodoApi (`samples/TodoApi/Program.cs`, `Models.cs`) responde al login con `LoginResponse(string Token)`, que se serializa como `{"token":"..."}` en camelCase. Así, `{{response.json.token}}` lo encuentra, ya que la búsqueda distingue mayúsculas y minúsculas.
- `SendApiRequestUseCase` ya extrae las VarsOut y las guarda de forma atómica en el ámbito ganador. El entorno gana sobre la colección. `ApiResponse` expone `AssignedVars` y `VarOutErrors`.
- `IIdGenerator` (`Application/Ports/IIdGenerator.cs`) solo genera ids de petición y colección; `GuidIdGenerator` los implementa.
- `MessagesTests` exige que cada constante de `UiMessageKeys` esté en `Messages.resx` y `Messages.en.resx`.

## Pasos
1. **Id de entorno**
   - `IIdGenerator`: añadir `Result<ApiEnvironmentId> NextApiEnvironmentId()`.
   - `GuidIdGenerator`: implementarlo igual que los otros (`Guid.NewGuid().ToString("N")`).
   - `GuidIdGeneratorTests`: dos tests, uno de formato GUID N y otro de ids distintos, como los existentes.

2. **`ApiTuiApp`**
   - Sustituir `CreateSampleRequest()` por una preparación que devuelva la petición y el id del entorno, por ejemplo `Result<(ApiRequest Request, ApiEnvironmentId EnvironmentId)>` o un `record` privado:
     - Crear el entorno `Demo` con `VarValue token = ""` (`new StringWithVars("")`) y guardarlo con `_store.Add(...)`.
     - Crear la petición como ahora, con `varsOut: [token = {{response.json.token}}]`. Hace falta `VarName.Create("token")` y `VarValue.Create(...)`, propagando errores con el patrón `is Error` que ya se usa.
     - Exponer las constantes públicas `TokenVarName = "token"` y `TokenVarOut = "{{response.json.token}}"` para los tests.
   - `RunAsync`:
     - Llamar a `ExecuteAsync(request, environmentId, ct)`.
     - Después de imprimir el cuerpo, mostrar cada error de `response.VarOutErrors` con `Ui.VarOutFailed` (`"{0}: {1}"`: nombre y `_messages.Format(error)`).
     - Por último, leer el entorno con `_store.FindAsync(environmentId, ct)`, buscar la variable `token` y mostrar `Ui.VariableValue` (`"Variable {0} = {1}"`).
     - Si el entorno o la variable no existen, que no debería ocurrir, se muestra el valor vacío. No se lanza ninguna excepción.

3. **Localización**
   - `Localization/UiMessageKeys.cs`: añadir `VariableValue = "Ui.VariableValue"` y `VarOutFailed = "Ui.VarOutFailed"`.
   - `Messages.resx` (es):
     - `Ui.VariableValue` → `Variable {0} = {1}`
     - `Ui.VarOutFailed` → `Error al extraer {0}: {1}`
   - `Messages.en.resx` (en):
     - `Ui.VariableValue` → `Variable {0} = {1}`
     - `Ui.VarOutFailed` → `Failed to extract {0}: {1}`
   - Revisar si `MessagesTests.Catalogs_PlaceholdersMatch...` necesita registrar el número de marcadores de las claves nuevas.

4. **Tests** (`tests/ApiTUI.Domain.Tests/ApiTuiAppTests.cs`)
   - `Run_SuccessfulResponse_...`: la respuesta falsa ya es `{"token":"abc"}`. Comprobar además que la salida contiene `Variable token = abc`.
   - Nuevo test: si la respuesta no trae `token`, por ejemplo `{}`, la salida muestra el error traducido de `VarOutPathNotFound` y `Variable token = ` (vacía).
   - `Run_Failure_...`: no se muestra la línea de la variable, porque se sale antes.
   - Mantener `[Collection("Language configuration")]`.

## Verificación
- `dotnet build ApiTUI/ApiTUI.slnx --configuration Release` sin advertencias.
- `dotnet test ApiTUI/ApiTUI.slnx -c Release`. Se usa Release porque la TodoApi en Debug puede bloquear su `.exe`.
- `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`.
- Prueba manual:
  1. Arrancar `dotnet run --project ApiTUI/samples/TodoApi`.
  2. Ejecutar `dotnet run --project ApiTUI/ApiTUI`.
  3. Al final debe aparecer `Variable token = <hex de 64 caracteres>`.
- Publicación Native AOT para win-x64 sin advertencias IL2xxx/IL3xxx.

## Riesgos / cosas a vigilar
- La lectura final se hace del almacén, no de `AssignedVars`, para demostrar que el valor quedó guardado.
- No añadir `token` a la colección: se crea solo en el entorno, para que sea el ámbito ganador.
- `IIdGenerator` cambia: hay que revisar si algún test u otra implementación (fakes) lo implementa.
- El token se imprime en consola. Es aceptable para esta prueba local, pero no debe reproducirse en la futura TUI sin decidirlo antes.

## Fuera de alcance
- Usar el token en peticiones posteriores (por ejemplo `Authorization: Bearer {{token}}` contra `/todos`).
- La TUI con Terminal.Gui y la persistencia en disco.
