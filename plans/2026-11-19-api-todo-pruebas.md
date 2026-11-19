# Plan: API TODO de prueba (Minimal API, .NET 11) para probar ApiTUI

Fecha: 2026-11-19
Estado: implementado

## Objetivo
Crear una Web API TODO muy sencilla en ASP.NET Core Minimal APIs (.NET 11, C# 15) que sirva de servidor de pruebas para ApiTUI:
- Persistencia en memoria, con un seed de tareas al arrancar.
- Usuario fijo `user2` / contraseña `user2`.
- Endpoints: login, addTask, removeTask, getTasks, getTask (por id), updateTask (por id).
- El login devuelve un token. Los endpoints de tareas exigen ese token; si falta o no es válido → **401**.

## Contexto
- **Decisión del usuario: la API TODO va en .NET 11 (`net11.0`, C# 15) y usa novedades de .NET 11 / C# 15.** El SDK `11.0.100` ya está instalado. La consola ApiTUI y sus tests siguen en `net10.0`; no se tocan.
- Novedades que se usan (ver paso 0):
  - **C# 15, union types** (`union`): resultados de las operaciones del almacén, con `switch` exhaustivo hacia `TypedResults`.
  - **C# 15, argumentos en expresiones de colección** (`[with(...)]`): crear diccionarios/sets con comparador o capacidad.
  - **ASP.NET Core 11, validación de Minimal APIs** (`AddValidation()` + DataAnnotations, generada en compilación y compatible con AOT): el 400 por título vacío sale de la validación, no de código manual.
  - **ASP.NET Core 11, OpenAPI 3.2** (`AddOpenApi()` / `MapOpenApi()`): documento en `/openapi/v1.json`, útil para montar la colección en ApiTUI.
- La solución es `ApiTUI/ApiTUI.slnx` (hoy: `ApiTUI/ApiTUI.csproj` + `tests/ApiTUI.Domain.Tests`). Decisión del usuario: la API va en `ApiTUI/samples/TodoApi`, **incluida en la slnx**, y lleva **tests de integración con `WebApplicationFactory`**.
- Reglas de `.claude/skills/csharp-skill/SKILL.md`: `record` para datos, clases `sealed`, constructores primarios con campos `_readonly`, sin `!` ni supresión de avisos de nulabilidad, `System.Text.Json` con `JsonSerializerContext` (AOT-friendly), YAGNI/KISS.
- El proyecto es una herramienta de pruebas: el token es un valor opaco aleatorio guardado en memoria. No se usa JWT ni `Microsoft.AspNetCore.Authentication.JwtBearer` (YAGNI y menos problemas con AOT/trimming).
- Los tests existentes usan xunit 2.9.3, xunit.runner.visualstudio 3.1.4, Microsoft.NET.Test.Sdk 17.14.1 y coverlet.collector 6.0.4 (`ApiTUI/tests/ApiTUI.Domain.Tests/ApiTUI.Domain.Tests.csproj`).

## Pasos

0. **Comprobar las novedades en el SDK instalado** antes de escribir código: `dotnet --version` (11.0.x) y un proyecto de prueba mínimo (fuera del repo) que compile un `union` y un `[with(...)]` con el `LangVersion` por defecto de `net11.0`.
   - Si `union` sigue siendo preview en C# 15 GA, poner `<LangVersion>preview</LangVersion>` **solo** en `TodoApi.csproj` y documentarlo en un comentario del csproj. Si con preview tampoco compila, sustituirlo por `record` base + `switch` y dejarlo anotado en el README (no bloquear la tarea).

1. **Proyecto** `ApiTUI/samples/TodoApi/TodoApi.csproj`:
   - `Sdk="Microsoft.NET.Sdk.Web"`, `net11.0`, `ImplicitUsings`, `Nullable` enable, `InvariantGlobalization` true, `PublishAot` true (activa los analizadores de AOT y el generador de delegados de Minimal APIs).
   - Paquete: `Microsoft.AspNetCore.OpenApi` (última 11.0.x). Nada más.
   - `Properties/launchSettings.json` con perfil `http` en `http://localhost:5080`.

2. **Modelos** (`Models.cs` o un fichero por tipo, todos `record`):
   - `TodoTask(int Id, string Title, bool IsCompleted)`.
   - `LoginRequest(string Username, string Password)`, `LoginResponse(string Token)`.
   - `CreateTaskRequest([property: Required, MinLength(1)] string Title)`, `UpdateTaskRequest([property: Required, MinLength(1)] string Title, bool IsCompleted)`. Validación de solo espacios: atributo propio sencillo `NotWhiteSpaceAttribute : ValidationAttribute` o `IValidatableObject` en el record (elegir lo que soporte el generador de validación de ASP.NET Core 11).
   - Union types (C# 15) para los resultados del almacén:
     ```csharp
     public sealed record TaskNotFound(int Id);
     public sealed record TaskRemoved(int Id);
     public union FindTaskResult(TodoTask, TaskNotFound);
     public union RemoveTaskResult(TaskRemoved, TaskNotFound);
     ```
     `Find`/`Update` devuelven `FindTaskResult` y `Remove` devuelve `RemoveTaskResult`. Los handlers usan `switch` exhaustivo: `TodoTask t => TypedResults.Ok(t)`, `TaskNotFound => TypedResults.NotFound()`.
   - `TodoJsonContext : JsonSerializerContext` con `[JsonSerializable]` para los tipos de request/response y `IReadOnlyList<TodoTask>`. Se registra con `builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, TodoJsonContext.Default))`.

3. **Almacén en memoria** `TodoStore` (`sealed`, singleton):
   - `Dictionary<int, TodoTask> _tasks = [with(capacity: 16)]` (C# 15) protegido con `Lock` (`System.Threading.Lock`) y contador `_nextId`.
   - Métodos: `GetAll()` (ordenado por Id), `Find(int id)` → `FindTaskResult`, `Add(string title)` → `TodoTask`, `Update(int id, string title, bool isCompleted)` → `FindTaskResult`, `Remove(int id)` → `RemoveTaskResult`.
   - **Seed**: método `Seed()` que añade 3 tareas de ejemplo (p. ej. "Probar ApiTUI", "Revisar documentación", "Preparar demo del TFM", con una marcada como completada). Se llama en `Program.cs` justo después de `builder.Build()`.

4. **Tokens** `TokenStore` (`sealed`, singleton):
   - `HashSet<string> _tokens = [with(StringComparer.Ordinal)]` (C# 15) protegido con `Lock`.
   - `Issue()` genera el token con `Convert.ToHexString(RandomNumberGenerator.GetBytes(32))`, lo guarda y lo devuelve.
   - `IsValid(string token)`.
   - Credenciales: constantes `Username = "user2"` y `Password = "user2"` en una clase estática `DemoCredentials` (comentario: solo para pruebas).

5. **Endpoints** en `Program.cs` (`WebApplication.CreateSlimBuilder`):
   - `builder.Services.AddValidation()`, `builder.Services.AddOpenApi()` (OpenAPI 3.2 por defecto en .NET 11) y `app.MapOpenApi()` (público, sin token, para que ApiTUI pueda leerlo).
   - Los `400` de `POST`/`PUT` los genera la validación de Minimal APIs (`ValidationProblem`). Los handlers no comprueban el título a mano.
   - `POST /login`, con cuerpo `LoginRequest`. Credenciales correctas → `200 { "token": "..." }`; incorrectas → `401`.
   - Grupo `/tasks` con un **endpoint filter** (`RequireToken`) que lee `Authorization: Bearer <token>`. Si falta, no empieza por `Bearer ` o `TokenStore.IsValid` es falso → `TypedResults.Unauthorized()` sin ejecutar el handler.
     - `GET /tasks` (getTasks) → `200` con la lista.
     - `GET /tasks/{id:int}` (getTask) → `200` o `404`.
     - `POST /tasks` (addTask) → título vacío/espacios → `400` (validación); si no, `201 Created` con `Location: /tasks/{id}` y la tarea.
     - `PUT /tasks/{id:int}` (updateTask) → título vacío → `400` (validación); no existe → `404`; ok → `200` con la tarea.
     - `DELETE /tasks/{id:int}` (removeTask) → `204` o `404`.
   - Orden: el filtro de token se ejecuta antes que la validación, así que sin token siempre es `401`. Verificarlo en tests; si la validación corre antes, ajustar el orden de los filtros.
   - Usar `TypedResults` y handlers síncronos (el almacén es en memoria; no hay I/O que cancelar).
   - Al final: `public partial class Program;` para `WebApplicationFactory<Program>`.

6. **Fichero de pruebas manuales** `ApiTUI/samples/TodoApi/TodoApi.http` con login, captura del token y las cinco llamadas de tareas (más una sin token para ver el 401). Sirve también como referencia para montar la colección en ApiTUI.

7. **Tests de integración** `ApiTUI/tests/TodoApi.Tests/TodoApi.Tests.csproj`:
   - `net11.0`. Mismas versiones de xunit/test SDK/coverlet que `ApiTUI.Domain.Tests`, más `Microsoft.AspNetCore.Mvc.Testing` (última 11.0.x), `ProjectReference` a `TodoApi.csproj` y `<Using Include="Xunit" />`.
   - `WebApplicationFactory<Program>` (cada test con su propia factory, o `IClassFixture` cuidando el estado compartido).
   - Casos:
     - Login correcto devuelve un token no vacío; login incorrecto → 401.
     - Cada endpoint de `/tasks` sin token → 401; con token inventado → 401.
     - `GET /tasks` tras arrancar devuelve las 3 tareas del seed.
     - `GET /tasks/{id}` existente → 200; inexistente → 404.
     - `POST /tasks` → 201 + `Location`; título vacío → 400.
     - `PUT /tasks/{id}` → 200 con los cambios; inexistente → 404.
     - `DELETE /tasks/{id}` → 204, y luego `GET` → 404.
     - `PUT`/`POST` con título vacío **y sin token** → 401 (no 400).
     - `GET /openapi/v1.json` → 200 y el documento declara `"openapi": "3.2..."`.
   - Deserializar respuestas con `TodoJsonContext.Default.*` (sin reflexión).

8. **Solución** `ApiTUI/ApiTUI.slnx`: añadir una carpeta `/samples/` con `samples/TodoApi/TodoApi.csproj`, y `tests/TodoApi.Tests/TodoApi.Tests.csproj` dentro de `/tests/`.

9. **Documentación** (en español):
   - `README.md`: sección "API TODO de pruebas" con cómo arrancarla (`dotnet run --project ApiTUI/samples/TodoApi`), credenciales, tabla de endpoints y códigos de respuesta, requisito del SDK 11 y la estructura actualizada.
   - `AGENTS.md`: añadir el comando de arranque y mencionar el proyecto en "Estado actual".

## Verificación
1. `dotnet build ApiTUI/ApiTUI.slnx --configuration Release` sin avisos nuevos.
2. `dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes`.
3. `dotnet test ApiTUI/ApiTUI.slnx` (tests existentes + `TodoApi.Tests` en verde).
4. `dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64` (comprobación habitual del skill; no debe verse afectada) y `dotnet publish ApiTUI/samples/TodoApi/TodoApi.csproj -c Release -r win-x64` sin avisos `IL2xxx`/`IL3xxx`.
5. Manual: `dotnet run --project ApiTUI/samples/TodoApi` y con curl:
   - `curl -i http://localhost:5080/tasks` → 401.
   - `curl -s -X POST http://localhost:5080/login -H "Content-Type: application/json" -d '{"username":"user2","password":"user2"}'` → token.
   - `curl -i http://localhost:5080/tasks -H "Authorization: Bearer <token>"` → 200 con las 3 tareas del seed.

## Riesgos / cosas a vigilar
- **Mezcla de net10.0 y net11.0 en la misma slnx**: compila con el SDK 11, pero quien solo tenga el SDK 10 no podrá compilar la solución. Documentarlo en el README. No añadir `global.json` salvo que haga falta.
- **Union types** pueden requerir `LangVersion preview` o tener cambios de sintaxis (paso 0). No usarlos en tipos que se serializan a JSON.
- La validación generada debe detectar los records de parámetros; si no, marcarlos con el atributo que requiera ASP.NET Core 11 (p. ej. `[ValidatableType]`).
- `CreateSlimBuilder` + AOT: todo tipo de request/response debe estar en `TodoJsonContext`; si falta alguno, habrá un fallo en tiempo de ejecución o un aviso del generador.
- Estado compartido en tests: el almacén es singleton por host; no asumir el número de tareas en tests que mutan si comparten factory.
- `dotnet format` sobre la slnx incluye ahora los proyectos nuevos.
- El `Dockerfile` y el `.dockerignore` de ApiTUI no deben verse afectados por la carpeta `samples/` (comprobar que el contexto de build no la necesita).
- La API es solo para pruebas locales: credenciales fijas y tokens sin caducidad. Dejarlo indicado en un comentario y en el README.

## Fuera de alcance
- JWT, caducidad/revocación de tokens, logout, varios usuarios.
- Persistencia real (fichero/BD), paginación, filtros.
- Swagger UI, HTTPS, Docker para la API TODO.
- Validación asíncrona (novedad de .NET 11, pero no hay reglas que la necesiten).
- Migrar la consola ApiTUI a .NET 11 o cualquier otro cambio en ella.

## Resultado

- La API de ejemplo y sus pruebas de integración están incluidas en la solución. El SDK 11.0.100 compiló `union` y `[with(...)]` con la versión predeterminada de C# 15.
- La comprobación del Bearer se ejecuta en middleware antes de leer el JSON. `Required` valida los títulos vacíos y con espacios mediante `AddValidation()`. Se evitó `MinLength` porque su constructor genera el aviso de trimming IL2026. `AddProblemDetails()` devuelve el detalle de validación con `application/problem+json`.
- Compilación Release sin avisos, formato correcto y 363 tests superados, incluidos 11 de TodoApi. Las publicaciones Native AOT para Windows x64 de ambos proyectos terminaron sin avisos.
- Probado con `curl` en localhost mediante `dotnet run` y el ejecutable nativo. Las tareas sin token devuelven 401, el login devuelve un token, el listado autenticado contiene las tres tareas iniciales, un título con espacios devuelve 400 y el documento público usa OpenAPI 3.2.
