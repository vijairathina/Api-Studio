namespace ApiStudio.Domain.Entities;

public class RequestHistoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public string? StatusDescription { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public ApiRequest RequestSnapshot { get; set; } = new();
}

public class SingleRequestRunResult
{
    public Guid RequestId { get; set; }
    public string RequestName { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<AssertionResult> AssertionResults { get; set; } = [];
}

public class CollectionRunResult
{
    public Guid CollectionId { get; set; }
    public string CollectionName { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public List<SingleRequestRunResult> Results { get; set; } = [];
    public int TotalRequests => Results.Count;
    public int PassedRequests => Results.Count(r => r.IsSuccess && r.AssertionResults.All(a => a.Passed));
    public int FailedRequests => TotalRequests - PassedRequests;
    public long TotalElapsedMilliseconds => Results.Sum(r => r.ElapsedMilliseconds);
}
