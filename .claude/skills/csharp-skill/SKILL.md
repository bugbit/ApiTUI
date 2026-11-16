---
name: csharp-skill
description: Reglas de desarrollo C# de ApiTUI y verificación final (compilación Native AOT, dotnet format y dotnet test). Úsalo al escribir o modificar código C# o tests en este repositorio, o invócalo directamente para comprobar que el código cumple las reglas.
---

# Desarrollo C# en este repositorio

Se puede invocar de forma individual para revisar que el código escrito (incluido el código de tests) sigue estas reglas.

## Reglas

- Usa la versión de C# que fija el proyecto.
- Datos sin comportamiento: `record`. Clases: `sealed`, salvo que la especificación pida herencia.
- Prefiere la composición a la herencia.
- Todo método asíncrono recibe un `CancellationToken` y lo propaga.
- No uses `.Result`, `.Wait()`, `GetAwaiter().GetResult()` ni `async void`.

### Nulabilidad

- No declares como anulable (`?`) un valor que el dominio considera obligatorio.
- No uses el operador `!` para silenciar un aviso de nulabilidad.
- No desactives los avisos de nulabilidad, ni en línea (`#pragma`) ni en el proyecto (`NoWarn`). Si crees que un aviso es incorrecto, detente y explica por qué.

### Principios de diseño al revisar o proponer un cambio

- **YAGNI:** toda abstracción, proveedor, factoría u opción de configuración debe corresponder a un requisito de la especificación. Si no es así, elimínala o explica a qué requisito responde.
- **Segundo uso real:** una abstracción reutilizable se justifica por un segundo consumidor que existe hoy, no por uno que podría existir.
- **KISS:** prefiere la solución con menos decisiones que un lector deba seguir para entender una lectura y una escritura. Simple sigue significando correcto: la concurrencia, las escrituras fallidas y los resultados vacíos deben seguir cubiertos.
- **DRY:** centraliza una regla solo cuando sus consumidores deben cambiar a la vez. Dos números idénticos no son una regla; dos endpoints que aplican la misma validación, sí.
- En la PR, indica qué conocimiento se comparte y por qué sus consumidores evolucionan juntos.

### Constructores primarios

Usa **constructores primarios** para la inyección de dependencias:

- los parámetros del constructor **nunca** se usan directamente en métodos o propiedades;
- cada dependencia se asigna a un campo `readonly` con prefijo `_`;
- dentro del cuerpo de la clase solo se usa ese campo;
- no uses `in`, `ref` ni `out` en los parámetros del constructor: el contenedor de DI no puede resolverlos (buscaría `IHello&` en lugar de `IHello`) y con tipos referencia no aportan nada.

Si el compilador emite **CS9124**, se está usando el parámetro además del campo: sustitúyelo por el campo.

Ejemplo correcto:

```csharp
public interface IHello
{
    void SayHello();
}

public sealed class ApiTuiApp(IHello hello)
{
    private readonly IHello _hello = hello;

    public Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _hello.SayHello();
        return Task.CompletedTask;
    }
}
```

## Antes de dar la tarea por terminada

Ejecuta los comandos desde la raíz del repositorio. La solución es `ApiTUI/ApiTUI.slnx`.

1. **Compilación Native AOT.** Tiene que compilar en AOT. Si falla, revisa por qué falla el código y soluciónalo:

   ```bash
   dotnet publish ApiTUI/ApiTUI/ApiTUI.csproj -c Release -r win-x64
   ```

   - En Linux/macOS usa el RID correspondiente (`linux-x64`, `osx-arm64`, …).
   - Los avisos `IL2xxx` (trimming) e `IL3xxx` (AOT) cuentan como fallo: corrige el código (p. ej. `System.Text.Json` con `JsonSerializerContext` en vez de reflexión), no los suprimas.
   - En Windows, si el error es `"vswhere.exe" no se reconoce como un comando` / `link.exe ... código 123`, es un problema del entorno, no del código: añade `C:\Program Files (x86)\Microsoft Visual Studio\Installer` al `PATH` y repite. AOT en Windows requiere la carga de trabajo "Desarrollo para el escritorio con C++" de Visual Studio.

2. **Formato:**

   ```bash
   dotnet format ApiTUI/ApiTUI.slnx --verify-no-changes
   ```

   Si falla, ejecuta `dotnet format ApiTUI/ApiTUI.slnx` y revisa los cambios.

3. **Tests:**

   ```bash
   dotnet test ApiTUI/ApiTUI.slnx
   ```

4. Si algo falla, corrige la causa y repite las tres comprobaciones.
