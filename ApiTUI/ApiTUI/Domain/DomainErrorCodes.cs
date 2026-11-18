namespace ApiTUI.Domain;

public static class DomainErrorCodes
{
    public const string IdentifierRequired = "Identifier.Required";
    public const string ApiRequestIdRequired = "ApiRequest.IdRequired";
    public const string ApiRequestLocationRequired = "ApiRequest.LocationRequired";
    public const string ApiRequestMethodInvalid = "ApiRequest.MethodInvalid";
    public const string ApiRequestHeaderNull = "ApiRequest.HeaderNull";
    public const string ApiCollectionIdRequired = "ApiCollection.IdRequired";
    public const string ApiFolderIdRequired = "ApiFolder.IdRequired";
    public const string ApiFolderLocationRequired = "ApiFolder.LocationRequired";
    public const string ApiFolderParentIsSelf = "ApiFolder.ParentIsSelf";
    public const string ItemNameEmpty = "ItemName.Empty";
    public const string ItemNameInvalidChars = "ItemName.InvalidChars";
    public const string ItemNameEndsWithDot = "ItemName.EndsWithDot";
    public const string ItemNameReserved = "ItemName.Reserved";
    public const string HeaderNameEmpty = "HeaderName.Empty";
    public const string HeaderNameInvalidChars = "HeaderName.InvalidChars";
    public const string HeaderValueNameRequired = "HeaderValue.NameRequired";
    public const string VarNameEmpty = "VarName.Empty";
    public const string VarNameInvalidChars = "VarName.InvalidChars";
    public const string VarValueNameRequired = "VarValue.NameRequired";
}
