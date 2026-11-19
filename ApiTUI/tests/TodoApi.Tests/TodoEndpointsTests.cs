using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TodoApi.Tests;

public sealed class TodoEndpointsTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();

    [Fact]
    public async Task Login_RejectsInvalidCredentials()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/login", new LoginRequest("user2", "incorrect"),
            TodoJsonContext.Default.LoginRequest, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/tasks")]
    [InlineData("GET", "/tasks/1")]
    [InlineData("POST", "/tasks")]
    [InlineData("PUT", "/tasks/1")]
    [InlineData("DELETE", "/tasks/1")]
    public async Task Tasks_RequireValidTokenBeforeReadingBody(string method, string path)
    {
        using var client = _factory.CreateClient();
        foreach (var token in new string?[] { null, "invalid" })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (method is "POST" or "PUT")
            {
                request.Content = new StringContent("{", Encoding.UTF8, "application/json");
            }

            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await client.SendAsync(request, CancellationToken.None);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Bearer");
        }
    }

    [Fact]
    public async Task Tasks_ListSeedAndFindById()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, CancellationToken.None);

        var tasks = await client.GetFromJsonAsync("/tasks", TodoJsonContext.Default.IReadOnlyListTodoTask, CancellationToken.None);
        Assert.NotNull(tasks);
        Assert.Equal(new[] { 1, 2, 3 }, tasks.Select(task => task.Id));
        Assert.Single(tasks, task => task.IsCompleted);

        var task = await client.GetFromJsonAsync("/tasks/1", TodoJsonContext.Default.TodoTask, CancellationToken.None);
        Assert.Equal(tasks[0], task);
        using var missing = await client.GetAsync("/tasks/999", CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Tasks_CreateUpdateAndDelete()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, CancellationToken.None);

        using var create = await client.PostAsJsonAsync("/tasks", new CreateTaskRequest("New task"),
            TodoJsonContext.Default.CreateTaskRequest, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(new Uri("/tasks/4", UriKind.Relative), create.Headers.Location);
        Assert.Equal(new TodoTask(4, "New task", false),
            await create.Content.ReadFromJsonAsync(TodoJsonContext.Default.TodoTask, CancellationToken.None));

        var changes = new UpdateTaskRequest("Updated task", true);
        using var update = await client.PutAsJsonAsync("/tasks/4", changes, TodoJsonContext.Default.UpdateTaskRequest, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(new TodoTask(4, "Updated task", true),
            await update.Content.ReadFromJsonAsync(TodoJsonContext.Default.TodoTask, CancellationToken.None));
        using var missingUpdate = await client.PutAsJsonAsync("/tasks/999", changes,
            TodoJsonContext.Default.UpdateTaskRequest, CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, missingUpdate.StatusCode);

        using var delete = await client.DeleteAsync("/tasks/4", CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var getAfterDelete = await client.GetAsync("/tasks/4", CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
        using var repeatDelete = await client.DeleteAsync("/tasks/4", CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, repeatDelete.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \t")]
    public async Task Tasks_InvalidTitleRequiresAuthenticationThenReturnsValidationProblem(string title)
    {
        using var client = _factory.CreateClient();
        using var anonymousPost = await client.PostAsJsonAsync("/tasks", new CreateTaskRequest(title),
            TodoJsonContext.Default.CreateTaskRequest, CancellationToken.None);
        using var anonymousPut = await client.PutAsJsonAsync("/tasks/1", new UpdateTaskRequest(title, true),
            TodoJsonContext.Default.UpdateTaskRequest, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPost.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPut.StatusCode);

        await LoginAsync(client, CancellationToken.None);
        using var post = await client.PostAsJsonAsync("/tasks", new CreateTaskRequest(title),
            TodoJsonContext.Default.CreateTaskRequest, CancellationToken.None);
        using var put = await client.PutAsJsonAsync("/tasks/1", new UpdateTaskRequest(title, true),
            TodoJsonContext.Default.UpdateTaskRequest, CancellationToken.None);
        await AssertTitleErrorAsync(post, CancellationToken.None);
        await AssertTitleErrorAsync(put, CancellationToken.None);
    }

    [Fact]
    public async Task OpenApi_DocumentIsPublicAndUsesVersion32()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(CancellationToken.None),
            cancellationToken: CancellationToken.None);
        Assert.StartsWith("3.2.", document.RootElement.GetProperty("openapi").GetString());
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/tasks", out _));
    }

    private static async Task LoginAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("/login", new LoginRequest("user2", "user2"),
            TodoJsonContext.Default.LoginRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync(TodoJsonContext.Default.LoginResponse, cancellationToken);
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.Token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
    }

    private static async Task AssertTitleErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        Assert.Contains(document.RootElement.GetProperty("errors").EnumerateObject(),
            error => error.Name.Equals("Title", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose() => _factory.Dispose();
}
