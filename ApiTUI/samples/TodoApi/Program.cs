using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddSingleton<TodoStore>();
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, TodoJsonContext.Default));

var app = builder.Build();
app.Services.GetRequiredService<TodoStore>().Seed();

// El middleware comprueba el token antes de que Minimal APIs lea el cuerpo.
app.UseWhen(context => context.Request.Path.StartsWithSegments("/tasks"), authenticated =>
    authenticated.Use(RequireToken));
app.MapOpenApi();

app.MapPost("/login", Results<Ok<LoginResponse>, UnauthorizedHttpResult> (LoginRequest request, TokenStore tokens) =>
    request.Username == DemoCredentials.Username && request.Password == DemoCredentials.Password
        ? TypedResults.Ok(new LoginResponse(tokens.Issue()))
        : TypedResults.Unauthorized());

var tasks = app.MapGroup("/tasks");
tasks.MapGet("", (TodoStore store) => TypedResults.Ok(store.GetAll())).Produces(StatusCodes.Status401Unauthorized);
tasks.MapGet("/{id:int}", (int id, TodoStore store) => ToHttpResult(store.Find(id))).Produces(StatusCodes.Status401Unauthorized);
tasks.MapPost("", (CreateTaskRequest request, TodoStore store) =>
{
    var task = store.Add(request.Title);
    return TypedResults.Created($"/tasks/{task.Id}", task);
}).Produces(StatusCodes.Status401Unauthorized).ProducesValidationProblem();
tasks.MapPut("/{id:int}", (int id, UpdateTaskRequest request, TodoStore store) =>
    ToHttpResult(store.Update(id, request.Title, request.IsCompleted)))
    .Produces(StatusCodes.Status401Unauthorized).ProducesValidationProblem();
tasks.MapDelete("/{id:int}", Results<NoContent, NotFound> (int id, TodoStore store) => store.Remove(id) switch
{
    TaskRemoved => TypedResults.NoContent(),
    TaskNotFound => TypedResults.NotFound(),
}).Produces(StatusCodes.Status401Unauthorized);

app.Run();

static Task RequireToken(HttpContext context, RequestDelegate next)
{
    const string prefix = "Bearer ";
    var header = context.Request.Headers.Authorization;
    if (header.Count == 1 && header[0] is { } authorization
        && authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && context.RequestServices.GetRequiredService<TokenStore>().IsValid(authorization[prefix.Length..]))
    {
        return next(context);
    }

    context.Response.Headers.WWWAuthenticate = "Bearer";
    return TypedResults.Unauthorized().ExecuteAsync(context);
}

static Results<Ok<TodoTask>, NotFound> ToHttpResult(FindTaskResult result) => result switch
{
    TodoTask task => TypedResults.Ok(task),
    TaskNotFound => TypedResults.NotFound(),
};

public sealed partial class Program;
