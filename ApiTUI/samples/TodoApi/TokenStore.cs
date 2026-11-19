using System.Security.Cryptography;

namespace TodoApi;

// Credenciales fijas y tokens sin caducidad para pruebas locales.
internal static class DemoCredentials
{
    public const string Username = "user2";
    public const string Password = "user2";
}

internal sealed class TokenStore
{
    private readonly Lock _lock = new();
    private readonly HashSet<string> _tokens = [with(StringComparer.Ordinal)];

    public string Issue()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        lock (_lock)
        {
            _tokens.Add(token);
        }

        return token;
    }

    public bool IsValid(string token)
    {
        lock (_lock)
        {
            return _tokens.Contains(token);
        }
    }
}
