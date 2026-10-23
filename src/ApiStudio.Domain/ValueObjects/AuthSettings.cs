using ApiStudio.Domain.Enums;

namespace ApiStudio.Domain.ValueObjects;

public class AuthSettings
{
    public AuthType Type { get; set; } = AuthType.None;
    
    // Bearer Token
    public string BearerToken { get; set; } = string.Empty;

    // Basic Auth
    public string BasicUsername { get; set; } = string.Empty;
    public string BasicPassword { get; set; } = string.Empty;

    // API Key
    public string ApiKeyName { get; set; } = string.Empty;
    public string ApiKeyValue { get; set; } = string.Empty;
    public ApiKeyLocation ApiKeyLocation { get; set; } = ApiKeyLocation.Header;

    public AuthSettings Clone()
    {
        return new AuthSettings
        {
            Type = Type,
            BearerToken = BearerToken,
            BasicUsername = BasicUsername,
            BasicPassword = BasicPassword,
            ApiKeyName = ApiKeyName,
            ApiKeyValue = ApiKeyValue,
            ApiKeyLocation = ApiKeyLocation
        };
    }
}
