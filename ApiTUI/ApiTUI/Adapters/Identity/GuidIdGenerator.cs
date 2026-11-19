using System;
using ApiTUI.Application.Ports;
using ApiTUI.Common;
using ApiTUI.Domain;

namespace ApiTUI.Adapters.Identity;

public sealed class GuidIdGenerator : IIdGenerator
{
    public Result<ApiRequestId> NextApiRequestId()
    {
        return ApiRequestId.Create(Guid.NewGuid().ToString("N"));
    }

    public Result<ApiCollectionId> NextApiCollectionId()
    {
        return ApiCollectionId.Create(Guid.NewGuid().ToString("N"));
    }

    public Result<ApiEnvironmentId> NextApiEnvironmentId()
    {
        return ApiEnvironmentId.Create(Guid.NewGuid().ToString("N"));
    }
}
