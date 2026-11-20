# Plan: Petición GET /tasks con el token capturado en ApiTuiApp

Fecha: 2026-11-20
Estado: pendiente de implementar

## Objetivo
En la prueba provisional por consola (`ApiTuiApp`), después de mostrar `Variable token = …`, hacer una segunda petición `GET {{url}}/tasks` autenticada con el token capturado en el login, y mostrar su respuesta con el mismo formato que la del login.

## Contexto
- `ApiTUI/ApiTUI/ApiTuiApp.cs`:
  - `RunAsync` prepara una colección `Demo` (`url = http://localhost:5080`) y un entorno `Demo` con `token = ""`.
  - Envía `POST {{url}}/login` con el VarOut `token = TokenVarOut`, muestra la respuesta y los errores de VarsOut, y lee `token` del entorno guardado.
- **Cambio sin commitear del usuario** que hay que conservar: `TokenVarOut = "Bearer {{response.json.token}}"`. Así, `token` ya contiene el valor completo de la cabecera (`Bearer <hex>`).
- TodoApi (`samples/TodoApi/Program.cs`):
  - `GET /tasks` exige `Authorization: Bearer <token>`; si falta, responde 401 con `WWW-Authenticate: Bearer`.
  - Devuelve la lista de tareas en JSON. Al arrancar carga tres tareas.
- `SendApiRequestUseCase.ExecuteAsync(request, environmentId, ct)` vuelve a cargar las variables del almacén en cada llamada, así que la segunda petición ve el `token` ya actualizado.
- Las cabeceras habilitadas se resuelven con variables (`{{token}}`).
- `ApiTuiAppTests` usa un `FakeHttpSender` que devuelve siempre el mismo resultado y comprueba `Assert.Single(sender.Requests)` y `EndsWith("Variable token = abc")`. Ese test ya no cuadra con el cambio a `Bearer …` y hay que actualizarlo.

## Pasos
1. **`ApiTuiApp.cs`**
   - Nueva constante `TasksUrl = BaseUrl + "/tasks"`.
   - Extraer a `WriteResponse(ApiResponse response)` el bloque que imprime estado, duración, cabeceras y cuerpo, para reutilizarlo en las dos peticiones.
   - En `PrepareSampleLogin`, que pasa a llamarse `PrepareSample`, crear también la petición de tareas en la misma colección:
     - Id nuevo con `_idGenerator.NextApiRequestId()`.
     - Nombre `Tasks`, `RequestMethod.Get` y URL `{{url}}/tasks`.
     - Cabeceras `Accept: application/json` (`CreateJsonHeader`) y `Authorization: {{token}}`, esta última con `HeaderName.Create("Authorization")` + `HeaderValue.Create(...)`.
     - Sin cuerpo y sin VarsOut.
     - El `record SampleLogin` pasa a ser `Sample(ApiRequest Login, ApiRequest Tasks, ApiEnvironmentId EnvironmentId)`.
   - En `RunAsync`, después de imprimir `Variable token = …`:
     - Imprimir `Ui.Request` con `GET {TasksUrl}`.
     - `await _sendApiRequest.ExecuteAsync(tasks, environmentId, ct)`.
     - Si devuelve `Error`, `WriteError`. Si no, `WriteResponse`.
   - La petición de tareas se envía siempre que el login haya devuelto respuesta, aunque falle el VarOut. En ese caso TodoApi responde 401, que se muestra igual y sirve de diagnóstico.
   - Si el login devuelve `Error` (por ejemplo, la conexión falla), se sale como ahora, sin pedir las tareas.

2. **Tests** (`tests/ApiTUI.Domain.Tests/ApiTuiAppTests.cs`)
   - `FakeHttpSender`: aceptar una secuencia de resultados (`params Result<HttpResponseData>[]`). Cada llamada consume el siguiente; el último se repite si hay más llamadas. Sigue registrando `Requests`.
   - `Run_SuccessfulResponse_...` pasa a llamarse `Run_SuccessfulLogin_SendsTasksWithCapturedToken`:
     - Login `{"token":"abc"}` y tareas `[{"id":1,"title":"Demo","isCompleted":false}]`.
     - Comprobar que hay 2 peticiones.
     - La 2.ª es `GET http://localhost:5080/tasks`, con cabeceras `Accept: application/json` y `Authorization: Bearer abc`, y sin cuerpo.
     - La salida contiene `Variable token = Bearer abc`, seguido de `Petición: GET http://localhost:5080/tasks` y del cuerpo de tareas, en ese orden.
   - `Run_ResponseWithoutToken_...`:
     - Ahora la variable queda vacía y la 2.ª petición lleva `Authorization` vacío. La salida debe contener el error traducido y `Variable token = `.
     - El fake responde 401 a la 2.ª y la salida muestra `401`.
     - Sustituir los `EndsWith` por `Contains` u orden de índices.
   - `Run_Failure_...`: comprobar que solo hay 1 petición y que no aparece `/tasks`.
   - Nuevo: si la 2.ª petición devuelve `Error` (fallo HTTP), la salida muestra el error traducido después de la variable.
   - `Run_Cancelled_...`: sin cambios.

3. **README** (`README.md`, sección de ejecución de la consola)
   - Actualizar la frase que describe la prueba: hace login, guarda `Bearer <token>` en la variable de entorno `token` mediante VarsOut, la muestra, y después hace `GET /tasks` con `Authorization: {{token}}`, mostrando las tareas.

## Verificación
- `dotnet build ApiTUI/ApiTUI.slnx --configuration Release` sin advertencias.
- `dotnet test ApiTUI/ApiTUI.slnx -c Release`. Se usa Release porque la TodoApi en Debug bloquea su `.exe`.
- `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`.
- Prueba manual:
  1. Arrancar `dotnet run --project ApiTUI/samples/TodoApi`.
  2. Ejecutar `dotnet run --project ApiTUI/ApiTUI`.
  3. Debe aparecer `Variable token = Bearer <hex>`, seguido de `GET http://localhost:5080/tasks`, `Estado: 200 OK` y las tres tareas sembradas.
- Publicación Native AOT para win-x64 sin advertencias IL2xxx/IL3xxx.

## Riesgos / cosas a vigilar
- **Cambio del usuario:** no revertir `TokenVarOut = "Bearer {{response.json.token}}"`. La cabecera es `Authorization: {{token}}`, sin añadir otro `Bearer`.
- **Cabecera vacía:** si el token queda vacío, la cabecera `Authorization` se envía vacía. `HttpClientSender` debe aceptarlo; hay que confirmarlo. Si lo rechaza, el error se muestra traducido y no rompe la app.
- **Token en consola:** se imprime el token completo. Se acepta para esta prueba local.
- **Formato:** reutilizar `WriteResponse` para no duplicar el formato de salida.

## Fuera de alcance
- Crear, modificar o borrar tareas.
- Parsear o formatear la lista de tareas; se muestra el cuerpo tal cual.
- La TUI con Terminal.Gui.
