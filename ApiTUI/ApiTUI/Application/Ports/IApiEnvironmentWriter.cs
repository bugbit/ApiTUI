using ApiTUI.Common;
using ApiTUI.Domain;

namespace ApiTUI.Application.Ports;

public interface IApiEnvironmentWriter
{
    Task<Result<IReadOnlyList<VarName>>> UpdateVariablesAsync(ApiEnvironmentId id,
        IReadOnlyDictionary<VarName, StringWithVars> values, CancellationToken cancellationToken);
}
