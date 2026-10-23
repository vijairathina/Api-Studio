namespace ApiStudio.Domain.Entities;

public class ApiResponse
{
    public int StatusCode { get; set; }
    public string StatusDescription { get; set; } = string.Empty;
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
    public long ElapsedMilliseconds { get; set; }
    public long ContentSizeBytes { get; set; }
    public List<KeyValuePair<string, string>> Headers { get; set; } = [];
    public List<string> Cookies { get; set; } = [];
    public string Body { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public List<AssertionResult> TestResults { get; set; } = [];

    public static ApiResponse Failure(string errorMessage, long elapsedMs = 0)
    {
        return new ApiResponse
        {
            StatusCode = 0,
            StatusDescription = "Error",
            ErrorMessage = errorMessage,
            ElapsedMilliseconds = elapsedMs,
            CompletedAt = DateTime.UtcNow
        };
    }
}
