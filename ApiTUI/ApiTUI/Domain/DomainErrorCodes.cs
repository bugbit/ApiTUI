namespace ApiTUI.Domain;

public static class DomainErrorCodes
{
    public const string IdentifierRequired = "Identifier.Required";
    public const string ApiRequestIdRequired = "ApiRequest.IdRequired";
    public const string ApiRequestLocationRequired = "ApiRequest.LocationRequired";
    public const string ApiRequestNameEmpty = "ApiRequest.NameEmpty";
    public const string ApiRequestNameInvalidChars = "ApiRequest.NameInvalidChars";
    public const string ApiRequestNameEndsWithDot = "ApiRequest.NameEndsWithDot";
    public const string ApiRequestNameReserved = "ApiRequest.NameReserved";
    public const string ApiRequestMethodInvalid = "ApiRequest.MethodInvalid";
    public const string ApiRequestHeaderNull = "ApiRequest.HeaderNull";
    public const string HeaderNameEmpty = "HeaderName.Empty";
    public const string HeaderNameInvalidChars = "HeaderName.InvalidChars";
    public const string HeaderValueNameRequired = "HeaderValue.NameRequired";
    public const string VarNameEmpty = "VarName.Empty";
    public const string VarNameInvalidChars = "VarName.InvalidChars";
    public const string VarValueNameRequired = "VarValue.NameRequired";
}
