using ApiTUI.Domain;

namespace ApiTUI.Application.Ports;

public interface IApiEnvironmentReader
{
    Task<ApiEnvironment?> FindAsync(ApiEnvironmentId id, CancellationToken cancellationToken);
}
