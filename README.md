# ApiTUI

Cliente HTTP interactivo para terminal, desarrollado en .NET con [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui). ApiTUI busca ofrecer un flujo de trabajo similar al de Postman en una interfaz de texto inspirada en las aplicaciones de consola clásicas, como Turbo Vision.

Las colecciones y peticiones se organizarán en una jerarquía de carpetas y se guardarán como ficheros JSON. El proyecto está planteado para publicarse como ejecutable nativo mediante Native AOT y como imagen Docker.

> **Estado del proyecto:** el dominio modela entornos, colecciones con variables, carpetas anidadas con sus propias variables y peticiones con nombres válidos para el sistema de archivos. La consola .NET 11 arranca con Generic Host, hace como prueba provisional un `POST /login` a la TodoApi local en `http://localhost:5080` con el caso de uso `SendApiRequestUseCase` y el adaptador con `HttpClient`, muestra por consola estado, duración, cabeceras y cuerpo, y termina. El envío resuelve variables de colección, entorno y carpetas desde un almacén en memoria. La TUI y la persistencia en disco siguen pendientes.

El envío admite cancelación y un límite de 100 segundos que cubre la respuesta completa. Las variables siguen la misma precedencia que [Bruno](https://docs.usebruno.com/variables/overview): la colección aporta los valores por defecto, el entorno seleccionado los sustituye y las carpetas aplican los suyos desde la raíz hasta la carpeta de la petición, que es la que más prioridad tiene. Las variables deshabilitadas se ignoran y un marcador válido sin valor genera un error. Las cabeceras deshabilitadas se ignoran y los valores con CR, LF o NUL se rechazan antes de enviar.

## Objetivos

- Crear, editar y ejecutar peticiones HTTP desde una interfaz de terminal.
- Organizar peticiones en colecciones y carpetas.
- Guardar los datos en ficheros JSON fáciles de inspeccionar, copiar y versionar.
- Distribuir la aplicación como ejecutable Native AOT y como contenedor Docker.
- Mantener una interfaz manejable con teclado y compatible con terminales habituales.

## Stack tecnológico

| Tecnología | Uso |
| --- | --- |
| C# 15 y .NET 11 | Lenguaje y plataforma de desarrollo. Los resultados de las operaciones usan union types (`Result<T>`). |
| `Microsoft.Extensions.Hosting` | Arranque, inyección de dependencias y ciclo de vida de la consola. |
| Terminal.Gui | Construcción de la interfaz TUI multiplataforma. |
| `System.Text.Json` | Lectura y escritura de colecciones, peticiones y configuración JSON. |
| Native AOT | Publicación de ejecutables nativos autocontenidos para RID compatibles. |
| Docker | Distribución y ejecución en un contenedor con terminal interactiva. |
| GitHub Actions | Automatización prevista para compilar y publicar artefactos e imágenes. |

Terminal.Gui dibuja la interfaz en la terminal; ApiTUI no requiere que el usuario instale ncurses. La referencia a ncurses y Turbo Vision describe el tipo de experiencia de usuario que se busca.

## Funcionalidades previstas

### Gestión de peticiones

- Crear, editar, duplicar y eliminar peticiones.
- Seleccionar el método HTTP: `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS` y `QUERY`.
- Configurar URL, parámetros de consulta y cabeceras.
- Enviar cuerpos de petición, incluyendo JSON y texto.
- Ejecutar la petición y cancelar una operación en curso cuando sea posible.

### Inspección de respuestas

- Consultar el código de estado y la duración de la respuesta.
- Inspeccionar cabeceras y cuerpo.
- Presentar JSON con formato legible.
- Mostrar errores de conexión y de configuración en la interfaz.

### Colecciones y almacenamiento

- Agrupar peticiones en colecciones y subcarpetas.
- Crear y renombrar colecciones desde la interfaz.
- Persistir los cambios en ficheros JSON.
- Permitir elegir la carpeta de datos para facilitar copias de seguridad y uso desde Docker.

### Interfaz de terminal

- Navegar por colecciones y peticiones con el teclado.
- Editar los campos de una petición y consultar su respuesta en paneles de la interfaz.
- Usar menús y atajos de teclado para las acciones frecuentes.
- Adaptar la vista al tamaño disponible de la terminal.

## Requisitos

Para compilar desde el código fuente:

- SDK de .NET 11 para compilar la solución completa (todos los proyectos apuntan a `net11.0`).
- Git.
- Un terminal que admita entrada interactiva y las secuencias de control usadas por Terminal.Gui.

Para ejecutar una publicación AOT, no hace falta instalar el runtime de .NET. La publicación debe corresponder al sistema operativo y arquitectura de destino.

Para ejecutar la imagen Docker, se necesita Docker y una terminal interactiva.

## Instalación y ejecución

### Clonar el repositorio

Cuando el repositorio esté creado, sustituye `<usuario>` por el propietario de GitHub:

```bash
git clone https://github.com/<usuario>/ApiTUI.git
cd ApiTUI
```

### Ejecutar desde el código fuente

Desde la raíz del repositorio:

```bash
dotnet restore ApiTUI/ApiTUI.slnx
dotnet build ApiTUI/ApiTUI.slnx --configuration Release
dotnet run --project ApiTUI/ApiTUI
```

Antes de ejecutar la consola, arranca TodoApi en otro terminal con `dotnet run --project ApiTUI/samples/TodoApi`. La consola hace un `POST` a `http://localhost:5080/login` y muestra la respuesta con el token. Si TodoApi está parada, muestra el error de conexión traducido. Usa `--Language en` para ver la salida en inglés.

### Publicar como Native AOT

Native AOT genera un ejecutable nativo para un *Runtime Identifier* (RID) concreto. Por ejemplo, para Linux x64:

```bash
dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  -p:PublishAot=true \
  -p:PublishSingleFile=true \
  --output ./publish/linux-x64
```

RIDs de ejemplo:

| Plataforma | RID de ejemplo |
| --- | --- |
| Windows x64 | `win-x64` |
| Linux x64 | `linux-x64` |
| Linux ARM64 | `linux-arm64` |
| macOS Apple Silicon | `osx-arm64` |
| macOS Intel | `osx-x64` |

Cada publicación AOT se compila para su plataforma de destino. El soporte final dependerá de los RIDs probados y de las dependencias utilizadas por la aplicación.

### Ejecutar con Docker

La interfaz necesita un terminal conectado. Por eso, el contenedor debe ejecutarse con `-it`:

```bash
docker build -t apitui:latest .
docker run --rm -it \
  -v "${PWD}/apitui-data:/data" \
  apitui:latest
```

El volumen permite conservar los ficheros JSON aunque se elimine el contenedor. La aplicación deberá configurarse para leer y escribir sus datos en `/data`; la opción concreta para indicar esa ruta se definirá durante la implementación.

En Windows PowerShell, el montaje puede expresarse así:

```powershell
docker run --rm -it `
  -v "${PWD}/apitui-data:/data" `
  apitui:latest
```

## Estructura actual y prevista

La solución actual está en `ApiTUI/ApiTUI.slnx`. Contiene la consola `ApiTUI/ApiTUI` y el proyecto `ApiTUI/tests/ApiTUI.Domain.Tests`, con pruebas de dominio, aplicación, adaptadores y localización. El dominio incluye entornos (`ApiEnvironment`), colecciones y carpetas con listas validadas de variables (`VarValueList`), además de peticiones; `ItemName` centraliza la validación de nombres. Dentro de la consola, `Application/SendApiRequest` coordina el envío a través del puerto `IHttpSender`; `Adapters/Http/HttpClientSender` implementa el transporte. Los lectores de entornos, colecciones y carpetas usan `Adapters/Persistence/InMemoryApiStore`. `Program.cs` configura Generic Host y valida el contenedor al arrancar; `ServiceCollectionExtensions.AddApiTui()` registra los servicios como singleton. Terminal.Gui ya está referenciado, pero todavía no se utiliza.

La separación en capas sigue prevista:

```text
ApiTUI/
├── src/
│   ├── ApiTUI/                       # Aplicación de consola y punto de entrada
│   ├── ApiTUI.Application/           # Casos de uso y coordinación de operaciones
│   ├── ApiTUI.Domain/                # Modelos y reglas del dominio
│   └── ApiTUI.Infrastructure/        # HTTP, persistencia JSON y configuración
├── tests/
│   ├── ApiTUI.Domain.Tests/
│   ├── ApiTUI.Application.Tests/
│   └── ApiTUI.Infrastructure.Tests/
├── docs/                             # Decisiones y documentación adicional
├── .github/
│   └── workflows/                    # Flujos de compilación y publicación
├── Dockerfile
├── .dockerignore
├── ApiTUI.sln
└── README.md
```

Esta estructura es una propuesta inicial. Si la aplicación crece, separa la interfaz, los casos de uso, el dominio y la infraestructura para que la persistencia y la comunicación HTTP puedan evolucionar sin acoplarlas a las vistas de Terminal.Gui.

### Datos de usuario

Los datos se guardarán fuera del código fuente. Una estructura posible sería:

```text
data/
├── collections/
│   └── Demo APIs/
│       ├── collection.json
│       └── requests/
│           ├── Get health.json
│           └── Create item.json
├── environments/
│   └── Local.json
└── settings.json
```

Cada fichero representará una entidad o configuración. La estructura definitiva y el esquema JSON se documentarán cuando se implementen los modelos y la migración de datos.

## Configuración

Los mensajes de error del dominio y los textos de la interfaz se traducen al español (`es`, por defecto) o al inglés (`en`) mediante la clase `Messages`. La prueba de consola usa ya estos textos y muestra los errores traducidos.

Para elegir el idioma:

```bash
dotnet run --project ApiTUI/ApiTUI -- --Language en
```

También se puede definir la variable de entorno `DOTNET_Language=en`. La opción `--Language` tiene prioridad sobre esa variable. Se aceptan variantes como `en-US`; un idioma desconocido usa español. Las traducciones están en `ApiTUI/ApiTUI/Localization/Messages.resx` (español, recurso neutro) y `Messages.en.resx` (inglés). Usan `ResourceManager`, el mismo mecanismo de recursos que Terminal.Gui. Añadir un idioma requiere crear `Messages.<idioma>.resx` y recompilar.

La cultura de interfaz se alinea con el idioma elegido. Terminal.Gui no incluye traducciones propias al español en la versión actual, así que sus botones y diálogos usarán inglés al elegir `es`. El formato de los mensajes sigue siendo invariante. Se mantiene `InvariantGlobalization=true` y se permite crear culturas por nombre con `PredefinedCulturesOnly=false`.

La carpeta de datos deberá poder configurarse para usar ubicaciones distintas en cada sistema operativo o para montar un volumen de Docker. La prioridad prevista es:

1. Ruta indicada por una opción de línea de comandos, si se implementa.
2. Variable de entorno, si se implementa.
3. Carpeta de datos de usuario del sistema operativo.

Los nombres y el orden definitivos se añadirán aquí cuando queden implementados. No guardes credenciales ni secretos en colecciones que vayas a subir a un repositorio público.

## Teclas y navegación

Los atajos se documentarán cuando estén definidos en la interfaz. ApiTUI se diseñará para que las acciones principales puedan realizarse desde el teclado y para que los atajos visibles en los menús sean coherentes con las convenciones de Terminal.Gui.

## Desarrollo

Desde la raíz del repositorio:

```bash
dotnet restore ApiTUI/ApiTUI.slnx
dotnet build ApiTUI/ApiTUI.slnx --configuration Release
dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes
dotnet test ApiTUI/ApiTUI.slnx
```

Los tests del envío usan dobles y no realizan conexiones de red. Los flujos de publicación siguen pendientes.

## Limitaciones previstas

- La experiencia depende de las capacidades del emulador de terminal, como el soporte de colores, entrada de teclado y tamaño de ventana.
- Los ejecutables Native AOT se generan por plataforma y arquitectura.
- Docker necesita una terminal interactiva para mostrar y controlar la interfaz TUI.
- Los secretos guardados en ficheros JSON quedan en texto legible salvo que se implemente cifrado o integración con el almacén seguro del sistema operativo.

## API TODO de pruebas

La API de ejemplo y sus pruebas de integración se compilan con el mismo SDK de .NET 11 que el resto de la solución.

Desde la raíz del repositorio, arranca el servidor local con:

```bash
dotnet run --project ApiTUI/samples/TodoApi
```

Escucha en `http://localhost:5080` y carga tres tareas en memoria. Usa `user2` como usuario y contraseña. Envía el token devuelto por el login en la cabecera `Authorization: Bearer <token>` para operar con las tareas. Las credenciales son fijas y los tokens duran hasta que se reinicia el servidor. Usa esta API solo para pruebas locales.

| Método y ruta | Cuerpo JSON | Respuestas |
| --- | --- | --- |
| `POST /login` | `username`, `password` | 200 con token, 401 |
| `GET /tasks` | | 200 con la lista, 401 |
| `GET /tasks/{id}` | | 200 con la tarea, 401, 404 |
| `POST /tasks` | `title` | 201 con `Location` y la tarea, 400, 401 |
| `PUT /tasks/{id}` | `title`, `isCompleted` | 200 con la tarea, 400, 401, 404 |
| `DELETE /tasks/{id}` | | 204, 401, 404 |
| `GET /openapi/v1.json` | | 200 con el documento OpenAPI 3.2 |

El servidor está en `ApiTUI/samples/TodoApi` y los tests con `WebApplicationFactory` en `ApiTUI/tests/TodoApi.Tests`. El fichero `TodoApi.http` incluye ejemplos que reutilizan el token y el identificador de la tarea creada.

## Licencia

ApiTUI se distribuye bajo la licencia MIT. Consulta el fichero [`LICENSE`](LICENSE) para ver el texto completo.
