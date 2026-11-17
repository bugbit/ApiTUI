using System.Net;
using ApiTUI.Adapters.Http;
using ApiTUI.Application;
using ApiTUI.Application.SendApiRequest;
using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

public sealed class HttpClientSenderTests
{
    [Theory]
    [InlineData(RequestMethod.Get, "GET")]
    [InlineData(RequestMethod.Post, "POST")]
    [InlineData(RequestMethod.Put, "PUT")]
    [InlineData(RequestMethod.Patch, "PATCH")]
    [InlineData(RequestMethod.Delete, "DELETE")]
    [InlineData(RequestMethod.Head, "HEAD")]
    [InlineData(RequestMethod.Options, "OPTIONS")]
    [InlineData(RequestMethod.Query, "QUERY")]
    public async Task Send_MapsEveryMethod(RequestMethod method, string expected)
    {
        string? actual = null;
        var handler = new FakeHandler((message, cancellationToken) =>
        {
            actual = message.Method.Method;
            Assert.Equal(new Uri("https://example.test/api"), message.RequestUri);
            Assert.Null(message.Content);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var client = Client(handler);

        var result = await Sender(client).SendAsync(Request(method), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Send_BodyAndHeaders_RoutesContentHeadersAndPreservesUtf8()
    {
        var handler = new FakeHandler(async (message, cancellationToken) =>
        {
            Assert.Equal(new[] { "1", "2" }, message.Headers.GetValues("X-Test"));
            Assert.NotNull(message.Content);
            Assert.Equal("application/json", message.Content.Headers.ContentType?.MediaType);
            Assert.Equal("café", await message.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = Client(handler);
        var request = Request(body: "café", headers:
            [new("X-Test", "1"), new("X-Test", "2"), new("Content-Type", "application/json")]);

        var result = await Sender(client).SendAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Send_BodyWithoutContentType_DoesNotImposeMediaType()
    {
        var handler = new FakeHandler((message, cancellationToken) =>
        {
            Assert.NotNull(message.Content);
            Assert.Null(message.Content.Headers.ContentType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var client = Client(handler);

        var result = await Sender(client).SendAsync(Request(body: "body"), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task Send_Response_ReturnsStatusHeadersAndContentEvenForHttpErrors(int statusCode)
    {
        var response = new HttpResponseMessage((HttpStatusCode)statusCode)
        {
            ReasonPhrase = "Example reason",
            Content = new StringContent("response body"),
        };
        response.Headers.TryAddWithoutValidation("X-Response", ["one", "two"]);
        response.Content.Headers.TryAddWithoutValidation("X-Content", "content-value");
        var handler = new FakeHandler((message, cancellationToken) => Task.FromResult(response));
        using var client = Client(handler);

        var result = await Sender(client).SendAsync(Request(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(statusCode, result.Value.StatusCode);
        Assert.Equal("Example reason", result.Value.ReasonPhrase);
        Assert.Equal("response body", result.Value.Body);
        Assert.Equal(new[] { "one", "two" }, result.Value.Headers.Where(pair => pair.Key == "X-Response").Select(pair => pair.Value));
        Assert.Contains(new KeyValuePair<string, string>("X-Content", "content-value"), result.Value.Headers);
        Assert.Contains(result.Value.Headers, pair => pair.Key == "Content-Type");
    }

    [Fact]
    public async Task Send_ConnectionFailure_ReturnsHttpFailure()
    {
        var handler = new FakeHandler((message, cancellationToken) => Task.FromException<HttpResponseMessage>(
            new HttpRequestException(HttpRequestError.NameResolutionError, "test")));
        using var client = Client(handler);

        var result = await Sender(client).SendAsync(Request(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.HttpFailure, result.Error.Reason);
        Assert.Equal(ApplicationErrorCodes.HttpRequestFailed, result.Error.Code);
        Assert.Equal("NameResolutionError", Assert.Single(result.Error.Args));
    }

    [Fact]
    public async Task Send_ConnectionLostDuringBodyRead_ReturnsHttpFailure()
    {
        var handler = new FakeHandler((message, cancellationToken) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new FailingStream()) }));
        using var client = Client(handler);

        var result = await Sender(client).SendAsync(Request(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.HttpFailure, result.Error.Reason);
        Assert.Equal(ApplicationErrorCodes.HttpRequestFailed, result.Error.Code);
        Assert.Equal("ResponseEnded", Assert.Single(result.Error.Args));
    }

    [Fact]
    public async Task Send_HandlerNeverResponds_TimesOut()
    {
        var handler = new FakeHandler(async (message, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = Client(handler);

        var result = await Sender(client, TimeSpan.FromMilliseconds(200)).SendAsync(Request(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorsReasons.Timeout, result.Error.Reason);
        Assert.Equal(ApplicationErrorCodes.HttpTimeout, result.Error.Code);
        Assert.Equal(0.2, Assert.Single(result.Error.Args));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Send_ResponseBodyNeverCompletes_TimesOutDuringRead()
    {
        var stream = new BlockingStream();
        var handler = new FakeHandler((message, cancellationToken) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }));
        using var client = Client(handler);

        var result = await Sender(client, TimeSpan.FromMilliseconds(200)).SendAsync(Request(), CancellationToken.None);

        Assert.True(stream.ReadStarted.Task.IsCompletedSuccessfully);
        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrorCodes.HttpTimeout, result.Error.Code);
        Assert.Equal(ErrorsReasons.Timeout, result.Error.Reason);
    }

    [Fact]
    public async Task Send_UserCancelsDuringBodyRead_PropagatesCancellation()
    {
        var stream = new BlockingStream();
        var handler = new FakeHandler((message, cancellationToken) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }));
        using var client = Client(handler);
        using var cancellation = new CancellationTokenSource();
        var task = Sender(client).SendAsync(Request(), cancellation.Token);
        await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Send_AlreadyCancelled_DoesNotCallHandler()
    {
        var handler = new FakeHandler((message, cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = Client(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sender(client).SendAsync(Request(), new CancellationToken(true)));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\0")]
    [InlineData("a\r\nX-Injected: 1")]
    public async Task Send_InvalidHeaderValue_RejectsBeforeCallingHandler(string value)
    {
        var handler = new FakeHandler((message, cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = Client(handler);

        var result = await Sender(client).SendAsync(Request(headers: [new("X-Test", value)]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrorCodes.HeaderValueInvalid, result.Error.Code);
        Assert.Equal("X-Test", Assert.Single(result.Error.Args));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("Content-Type")]
    [InlineData("Invalid Header")]
    public async Task Send_HeaderCannotBeAdded_ReturnsHeaderInvalid(string name)
    {
        var handler = new FakeHandler((message, cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = Client(handler);

        var result = await Sender(client).SendAsync(Request(headers: [new(name, "value")]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrorCodes.HeaderInvalid, result.Error.Code);
        Assert.Equal(ErrorsReasons.InvalidInput, result.Error.Reason);
        Assert.Equal(name, Assert.Single(result.Error.Args));
        Assert.Equal(0, handler.Calls);
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { Timeout = Timeout.InfiniteTimeSpan };

    private static HttpClientSender Sender(HttpClient client, TimeSpan? timeout = null) =>
        new(client, new HttpSenderOptions(timeout ?? TimeSpan.FromSeconds(30)), TimeProvider.System);

    private static HttpRequestData Request(RequestMethod method = RequestMethod.Get, string? body = null,
        IReadOnlyList<KeyValuePair<string, string>>? headers = null) =>
        new(method, new Uri("https://example.test/api"), headers ?? [], body);

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handle = handle;
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return _handle(request, cancellationToken);
        }
    }

    private sealed class BlockingStream : Stream
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // Simula que la conexión se corta a mitad del cuerpo, como hace SocketsHttpHandler. ReadAsStringAsync envuelve
    // la HttpIOException en una HttpRequestException que conserva el HttpRequestError.
    private sealed class FailingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new HttpIOException(HttpRequestError.ResponseEnded, "test"));

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
