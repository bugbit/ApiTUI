namespace ApiTUI.Common;

public sealed record Error
{
    public string Reason { get; }
    public string Code { get; }
    public IReadOnlyList<object> Args { get; }

    public Error(string reason, string code, params object[] args)
    {
        Reason = reason;
        Code = code;
        Args = Array.AsReadOnly(args.ToArray());
    }
}

// Resultado de una operación: contiene el valor (T) o el error que impidió obtenerlo.
public union Result<T>(Error, T);
