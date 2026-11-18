using ApiTUI.Domain;

namespace ApiTUI.Application.Ports;

public interface IApiCollectionReader
{
    Task<ApiCollection?> FindAsync(ApiCollectionId id, CancellationToken cancellationToken);
}
