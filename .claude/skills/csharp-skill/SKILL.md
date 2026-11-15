---
name: csharp-skill
description: Reglas de desarrollo C# de ApiTUI y verificación final (compilación Native AOT, dotnet format y dotnet test). Úsalo al escribir o modificar código C# o tests en este repositorio, o invócalo directamente para comprobar que el código cumple las reglas.
---

# C# development in this repository

Se puede invocar de forma individual para revisar que el código escrito (incluido el código de tests) sigue estas reglas.

## Reglas
- Use the C# version the project sets.
- Data without behaviour: record. Classes: sealed, unless the spec asks for inheritance.
- Prefer composition over inheritance.
- Do not declare a required value as nullable. Do not use the ! operator or disable warnings.
- Every async method receives a CancellationToken and passes it on.
- Do not use .Result, .Wait(), GetAwaiter().GetResult() or async void.

Nullability:

- Do not declare as nullable (?) a value the domain considers required.
- Do not use the ! operator to silence a nullability warning.
- Do not disable nullability warnings, neither inline (#pragma) nor in the project (NoWarn). If you think a warning is wrong, stop and explain why.

Design principles when reviewing or proposing a change:

- YAGNI: every abstraction, provider, factory or configuration option must point to a requirement in the spec. If it does not, remove it or explain the requirement it serves.
- Second real use: a reusable abstraction is justified by a second consumer that exists today, not by one that might exist.
- KISS: prefer the solution with the fewest decisions a reader must follow to understand one read and one write. Simple still means correct: concurrency, failed writes and empty results stay covered.
- DRY: centralise a rule only when its consumers must change together. Two identical numbers are not one rule. Two endpoints applying the same validation are.
- In the PR, state what knowledge is shared and why its consumers evolve together.

Usar **constructores primarios** para inyección de dependencias:

- los parámetros del constructor **nunca** se usan directamente en métodos o propiedades
- cada dependencia se asigna a un campo `readonly` con prefijo `_`
- solo se usa ese campo dentro del cuerpo de la clase

Ejemplo correcto:

```csharp
public sealed class TaskAppService(in ITaskRepository taskRepository)
{
    private readonly ITaskRepository _taskRepository = taskRepository;

    public Task<TaskItem?> GetByIdAsync(TaskId id, CancellationToken cancellationToken)
    {
        return _taskRepository.GetByIdAsync(id, cancellationToken);
    }
}
```

## Before you consider the task finished

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

4. If anything fails, fix the cause and repeat the three checks.
