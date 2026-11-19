using System;
using ApiTUI.Common;
using ApiTUI.Domain;

namespace ApiTUI.Application.Ports;

public interface IIdGenerator
{
    Result<ApiRequestId> NextApiRequestId();
    Result<ApiCollectionId> NextApiCollectionId();
    Result<ApiEnvironmentId> NextApiEnvironmentId();
}
