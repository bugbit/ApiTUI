namespace ApiTUI.Application;

public static class ApplicationErrorCodes
{
    public const string EnvironmentNotFound = "SendApiRequest.EnvironmentNotFound";
    public const string CollectionNotFound = "SendApiRequest.CollectionNotFound";
    public const string FolderNotFound = "SendApiRequest.FolderNotFound";
    public const string FolderCycle = "SendApiRequest.FolderCycle";
    public const string UnresolvedVariables = "SendApiRequest.UnresolvedVariables";
    public const string UrlEmpty = "SendApiRequest.UrlEmpty";
    public const string UrlInvalid = "SendApiRequest.UrlInvalid";
    public const string UrlSchemeNotSupported = "SendApiRequest.UrlSchemeNotSupported";
    public const string HeaderInvalid = "SendApiRequest.HeaderInvalid";
    public const string HeaderValueInvalid = "SendApiRequest.HeaderValueInvalid";
    public const string HttpTimeout = "Http.Timeout";
    public const string HttpRequestFailed = "Http.RequestFailed";
}
