# Plan: ApiTuiApp hace login contra la TodoApi local

Fecha: 2026-11-19
Estado: implementado

## Objetivo
Cambiar la prueba provisional de `ApiTuiApp` para que, en lugar del GET a jsonplaceholder, haga el login contra la TodoApi local:
- Variable de colección `url` = `http://localhost:5080`.
- Petición `POST {{url}}/login` con cuerpo JSON `{"username":"user2","password":"user2"}`.
- La respuesta esperada es `200` con `{"token":"..."}`, que se muestra por consola como hasta ahora.

## Contexto
- `ApiTUI/ApiTUI/ApiTuiApp.cs`:
  - Constante `SampleUrl = "https://jsonplaceholder.typicode.com/todos/1"`, que se usa como valor de la variable `url` y en el mensaje `Request` de la consola.
  - `CreateSampleRequest()` crea la colección "Demo" con la variable `url` y una petición `GET` con URL `{{url}}` y cabecera `Accept: application/json`.
- La TodoApi (`ApiTUI/samples/TodoApi`) escucha en `http://localhost:5080` con `dotnet run` (perfil `http` de `launchSettings.json`). El `.exe` publicado usa el puerto 5000 por defecto y hay que arrancarlo con `--urls http://localhost:5080`. `/login` es `POST` y su cuerpo es `LoginRequest(Username, Password)` con nombres en camelCase.
- `ApiRequest.CreateInCollection(..., method, url, headers, body)` ya admite cuerpo (`StringWithVars? body`). `HttpClientSender` (`Adapters/Http/HttpClientSender.cs`) envía el cuerpo como `ByteArrayContent` y coloca `Content-Type` en las cabeceras del contenido. No hace falta tocar el dominio, el caso de uso ni el adaptador.
- Tests afectados: `ApiTUI/tests/ApiTUI.Domain.Tests/ApiTuiAppTests.cs` (usa `ApiTuiApp.SampleUrl` y comprueba `GET`).

## Pasos

1. **`ApiTuiApp.cs`**:
   - Sustituir `SampleUrl` por:
     ```csharp
     public const string BaseUrl = "http://localhost:5080";
     public const string LoginUrl = BaseUrl + "/login";
     public const string LoginBody = """{"username":"user2","password":"user2"}""";
     ```
   - La variable `url` de la colección toma el valor `BaseUrl`.
   - La petición pasa a ser `RequestMethod.Post`, con nombre "Login", URL `new StringWithVars("{{url}}/login")` y cuerpo `new StringWithVars(LoginBody)`.
   - Cabeceras: `Accept: application/json` y `Content-Type: application/json` (sin la segunda, la TodoApi responde `415`).
   - El mensaje `Request` de la consola muestra `POST http://localhost:5080/login` (usar `LoginUrl`).
   - Actualizar el comentario de la clase: hace login contra la TodoApi local de `ApiTUI/samples/TodoApi`, que debe estar arrancada.

2. **`ApiTuiAppTests.cs`**:
   - El test de éxito pasa a comprobar `RequestMethod.Post`, `request.Url == new Uri(ApiTuiApp.LoginUrl)` (prueba que `{{url}}/login` se resuelve), las dos cabeceras (`Accept` y `Content-Type`) y `request.Body == ApiTuiApp.LoginBody`. La respuesta falsa devuelve `{"token":"abc"}`, y la salida contiene `Petición: POST http://localhost:5080/login` y el token.
   - `Run_Failure_...` comprueba `Request: POST {LoginUrl}`.
   - Renombrar el test de éxito a algo como `Run_SuccessfulResponse_SendsLoginAndPrintsResult`.

3. **Documentación** (en español):
   - `README.md` (línea del estado del proyecto y sección de ejecución, hacia la línea 97): la consola hace un `POST` de login a la TodoApi local en `http://localhost:5080`. Antes hay que arrancarla con `dotnet run --project ApiTUI/samples/TodoApi`. Si no está arrancada, se muestra el error de conexión traducido.
   - `AGENTS.md` (Estado actual): cambiar "hace un GET a una API pública" por "hace login contra la TodoApi de pruebas local".

## Verificación
1. `dotnet build ApiTUI/ApiTUI.slnx -c Release` sin avisos.
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes --no-restore`. Con el restore interno falla también en HEAD por un problema del SDK 11, ajeno a este cambio.
3. `dotnet test ApiTUI/ApiTUI.slnx` en verde.
4. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64` sin avisos `IL2xxx`/`IL3xxx`.
5. Manual:
   - Terminal 1: `dotnet run --project ApiTUI/samples/TodoApi`.
   - Terminal 2: `dotnet run --project ApiTUI/ApiTUI` → `POST http://localhost:5080/login`, `Estado: 200 OK` y el cuerpo con el token.
   - Con la TodoApi parada → mensaje de error de conexión traducido, sin excepción.

## Riesgos / cosas a vigilar
- Sin `Content-Type: application/json` la TodoApi devuelve `415`. Comprobar que `HttpClientSender` lo coloca en las cabeceras del contenido (ya lo hace cuando hay cuerpo).
- La consola deja de funcionar sin la TodoApi arrancada (antes dependía de Internet). Documentarlo.
- Los tests de `ApiTuiAppTests` usan un `IHttpSender` falso: no hacen red y no requieren la TodoApi.
- Credenciales en el código de la consola: aceptable porque es la prueba provisional contra una API de pruebas local.

## Fuera de alcance
- Usar el token en llamadas posteriores (`/tasks`).
- Parametrizar la URL o las credenciales por CLI o configuración.
- Cambios en la TodoApi, el dominio, el caso de uso o el adaptador HTTP.

## Resultado

- La demo resuelve `{{url}}/login` desde la colección y envía el POST como JSON. Los tests comprueban el método, la URL resuelta, las dos cabeceras, el cuerpo y la salida de consola.
- Pasaron los 363 tests, el formato y la compilación Release. Native AOT para Windows x64 compiló sin avisos.
- La instancia de TodoApi que ya escuchaba en el puerto 5080 devolvió HTTP 200 con un token tanto a `dotnet run` como al ejecutable nativo. Para comprobar los errores de conexión en español e inglés se usó un proxy local sin escucha, configurado solo en el proceso de prueba.
