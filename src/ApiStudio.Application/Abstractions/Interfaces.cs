using ApiStudio.Domain.Entities;

namespace ApiStudio.Application.Abstractions;

public interface IHttpRequestExecutor
{
    Task<ApiResponse> ExecuteAsync(ApiRequest request, CancellationToken cancellationToken = default);
}

public interface IVariableResolver
{
    string Resolve(string input, IDictionary<string, string> variables);
    ApiRequest ResolveRequest(ApiRequest request, IDictionary<string, string> variables);
}

public interface IAssertionEvaluator
{
    List<AssertionResult> Evaluate(ApiResponse response, IEnumerable<AssertionRule> rules);
}

public interface ICurlConverter
{
    ApiRequest ParseCurl(string curlCommand);
    string ExportCurl(ApiRequest request);
}

public interface ICollectionRepository
{
    Task<List<ApiCollection>> GetAllAsync();
    Task SaveAllAsync(IEnumerable<ApiCollection> collections);
    Task<ApiCollection?> GetByIdAsync(Guid id);
    Task AddAsync(ApiCollection collection);
    Task UpdateAsync(ApiCollection collection);
    Task DeleteAsync(Guid id);
}

public interface IEnvironmentRepository
{
    Task<List<ApiEnvironment>> GetAllAsync();
    Task SaveAllAsync(IEnumerable<ApiEnvironment> environments);
    Task<ApiEnvironment?> GetActiveAsync();
    Task SetActiveAsync(Guid? id);
}

public interface IHistoryRepository
{
    Task<List<RequestHistoryItem>> GetAllAsync();
    Task AddAsync(RequestHistoryItem item);
    Task ClearAsync();
}

public class UserPreferences
{
    public string Theme { get; set; } = "Dark";
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public bool FollowRedirects { get; set; } = true;
    public bool VerifySsl { get; set; } = true;
    public bool AutoFormatJson { get; set; } = true;
}

public interface IUserPreferencesRepository
{
    Task<UserPreferences> GetAsync();
    Task SaveAsync(UserPreferences preferences);
}

public interface ICollectionRunner
{
    Task<CollectionRunResult> RunAsync(
        ApiCollection collection, 
        IDictionary<string, string> variables, 
        Action<SingleRequestRunResult>? onProgress = null, 
        CancellationToken cancellationToken = default);
}

public interface IImportExportService
{
    string ExportCollection(ApiCollection collection);
    ApiCollection ImportCollection(string json);
    string ExportEnvironment(ApiEnvironment environment);
    ApiEnvironment ImportEnvironment(string json);
    bool IsPostmanCollection(string json);
    ApiCollection ImportPostmanCollection(string json);
}
