using ApiStudio.Domain.Enums;
using ApiStudio.Domain.ValueObjects;

namespace ApiStudio.Domain.Entities;

public class ApiRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Request";
    public string Description { get; set; } = string.Empty;
    public HttpMethodType Method { get; set; } = HttpMethodType.GET;
    public string CustomMethod { get; set; } = string.Empty;
    public string Url { get; set; } = "https://";
    public List<KeyValuePairItem> QueryParams { get; set; } = [];
    public List<KeyValuePairItem> Headers { get; set; } = [];
    public AuthSettings Auth { get; set; } = new();
    public RequestBodySettings Body { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 30;
    public bool FollowRedirects { get; set; } = true;
    public List<AssertionRule> Assertions { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    public string EffectiveMethod => Method == HttpMethodType.CUSTOM 
        ? (string.IsNullOrWhiteSpace(CustomMethod) ? "CUSTOM" : CustomMethod.ToUpperInvariant())
        : Method.ToString();

    public ApiRequest Clone()
    {
        return new ApiRequest
        {
            Id = Guid.NewGuid(),
            Name = Name,
            Description = Description,
            Method = Method,
            CustomMethod = CustomMethod,
            Url = Url,
            QueryParams = QueryParams.Select(x => x.Clone()).ToList(),
            Headers = Headers.Select(x => x.Clone()).ToList(),
            Auth = Auth.Clone(),
            Body = Body.Clone(),
            TimeoutSeconds = TimeoutSeconds,
            FollowRedirects = FollowRedirects,
            Assertions = Assertions.Select(x => x.Clone()).ToList(),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }
}
