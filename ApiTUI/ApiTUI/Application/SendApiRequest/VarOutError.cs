using ApiTUI.Common;
using ApiTUI.Domain;

namespace ApiTUI.Application.SendApiRequest;

public sealed record VarOutError(VarName Name, Error Error);
