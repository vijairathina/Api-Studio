namespace ApiStudio.Domain.Enums;

public enum HttpMethodType
{
    GET,
    POST,
    PUT,
    PATCH,
    DELETE,
    HEAD,
    OPTIONS,
    CUSTOM
}

public enum BodyType
{
    None,
    Json,
    Xml,
    Text,
    FormUrlEncoded,
    MultipartFormData,
    BinaryFile
}

public enum AuthType
{
    None,
    Bearer,
    Basic,
    ApiKey
}

public enum ApiKeyLocation
{
    Header,
    QueryParam
}

public enum AssertionTarget
{
    StatusCode,
    ResponseTimeMs,
    Header,
    BodyContains,
    JsonPath
}

public enum AssertionOperator
{
    Equals,
    NotEquals,
    Contains,
    DoesNotContain,
    GreaterThan,
    LessThan
}
