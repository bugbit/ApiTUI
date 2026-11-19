using ApiTUI.Common;
using ApiTUI.Domain;

namespace ApiTUI.Application.Ports;

public interface IApiFolderWriter
{
    Task<Result<IReadOnlyList<VarName>>> UpdateVariablesAsync(ApiFolderId id,
        IReadOnlyDictionary<VarName, StringWithVars> values, CancellationToken cancellationToken);
}
