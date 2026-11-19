using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TodoApi;

public sealed record TodoTask(int Id, string Title, bool IsCompleted);
public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(string Token);
public sealed record CreateTaskRequest([property: Required] string Title);
public sealed record UpdateTaskRequest([property: Required] string Title, bool IsCompleted);

public sealed record TaskNotFound(int Id);
public sealed record TaskRemoved(int Id);
public union FindTaskResult(TodoTask, TaskNotFound);
public union RemoveTaskResult(TaskRemoved, TaskNotFound);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(CreateTaskRequest))]
[JsonSerializable(typeof(UpdateTaskRequest))]
[JsonSerializable(typeof(TodoTask))]
[JsonSerializable(typeof(IReadOnlyList<TodoTask>))]
public sealed partial class TodoJsonContext : JsonSerializerContext;
