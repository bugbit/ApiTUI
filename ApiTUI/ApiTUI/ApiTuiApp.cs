namespace ApiTUI;

public sealed class ApiTuiApp
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine("Hello, World!");
        return Task.CompletedTask;
    }
}
