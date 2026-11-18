using ApiTUI.Domain;

namespace ApiTUI.Application.Ports;

public interface IApiFolderReader
{
    Task<ApiFolder?> FindAsync(ApiFolderId id, CancellationToken cancellationToken);
}
