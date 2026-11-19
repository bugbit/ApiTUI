using ApiTUI.Common;

namespace ApiTUI.Domain.Tests;

// Aserciones sobre el union Result<T>: comprueban el caso y devuelven su contenido ya tipado.
internal static class ResultAssert
{
    public static T AssertSuccess<T>(this Result<T> result) => Assert.IsAssignableFrom<T>(result.Value);

    public static Error AssertFailure<T>(this Result<T> result) => Assert.IsType<Error>(result.Value);
}
