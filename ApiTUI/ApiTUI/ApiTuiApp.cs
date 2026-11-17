namespace ApiTUI;

public interface IHello
{
    void SayHello();
}

public sealed class Hello() : IHello
{
    public void SayHello()
    {
        Console.WriteLine("Hello, World!");
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
