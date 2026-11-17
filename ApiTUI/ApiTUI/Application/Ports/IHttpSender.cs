using ApiTUI.Application.SendApiRequest;
using ApiTUI.Common;

namespace ApiTUI.Application.Ports;

public interface IHttpSender
{
    Task<Result<HttpResponseData>> SendAsync(HttpRequestData request, CancellationToken cancellationToken);
}
