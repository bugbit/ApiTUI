using ApiTUI.Localization;

namespace ApiTUI;

public interface IHello
{
    void SayHello();
}

public sealed class Hello(Messages messages) : IHello
{
    private readonly Messages _messages = messages;

    public void SayHello()
    {
        Console.WriteLine(_messages.Get(UiMessageKeys.HelloWorld));
    }
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
