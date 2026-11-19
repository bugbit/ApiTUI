using ApiTUI.Common;
using ApiTUI.Domain;

namespace ApiTUI.Application.Ports;

public interface IApiCollectionWriter
{
    Task<Result<IReadOnlyList<VarName>>> UpdateVariablesAsync(ApiCollectionId id,
        IReadOnlyDictionary<VarName, StringWithVars> values, CancellationToken cancellationToken);
}
