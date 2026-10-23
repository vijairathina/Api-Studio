using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;
using ApiStudio.Domain.ValueObjects;
using ApiStudio.Infrastructure.Http;
using ApiStudio.Infrastructure.Persistence;
using Xunit;

namespace ApiStudio.Infrastructure.Tests;

public class InfrastructureTests : IDisposable
{
    private readonly string _tempDir;
    private readonly HttpClientRequestExecutor _executor = new();

    public InfrastructureTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ApiStudioTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // ignore temp cleanup error
        }
    }

    [Fact]
    public async Task HttpClientRequestExecutor_ReturnsFriendlyError_WhenUrlEmpty()
    {
        var request = new ApiRequest { Url = "" };
        var response = await _executor.ExecuteAsync(request);

        Assert.Equal(0, response.StatusCode);
        Assert.NotNull(response.ErrorMessage);
        Assert.Contains("specify a request URL", response.ErrorMessage);
    }

    [Fact]
    public async Task HttpClientRequestExecutor_ReturnsFriendlyError_WhenUrlInvalidScheme()
    {
        var request = new ApiRequest { Url = "ftp://invalid-scheme.com" };
        var response = await _executor.ExecuteAsync(request);

        Assert.Equal(0, response.StatusCode);
        Assert.NotNull(response.ErrorMessage);
        Assert.Contains("Please check the request URL", response.ErrorMessage);
    }

    [Fact]
    public async Task HttpClientRequestExecutor_HandlesCancellationGracefully()
    {
        var request = new ApiRequest { Url = "https://httpstat.us/200?sleep=5000" };
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        var response = await _executor.ExecuteAsync(request, cts.Token);

        Assert.Equal(0, response.StatusCode);
        Assert.Contains("canceled", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JsonFileCollectionRepository_SeedsSampleCollectionOnFirstAccess()
    {
        var repo = new JsonFileCollectionRepository(_tempDir);
        var collections = await repo.GetAllAsync();

        Assert.NotEmpty(collections);
        Assert.Equal("Sample JSONPlaceholder API", collections[0].Name);
        Assert.NotEmpty(collections[0].Requests);
    }

    [Fact]
    public async Task JsonFileEnvironmentRepository_SeedsEnvironmentsAndTogglesActive()
    {
        var repo = new JsonFileEnvironmentRepository(_tempDir);
        var environments = await repo.GetAllAsync();

        Assert.Equal(3, environments.Count);
        var staging = environments.First(e => e.Name == "Staging");

        await repo.SetActiveAsync(staging.Id);
        var active = await repo.GetActiveAsync();

        Assert.NotNull(active);
        Assert.Equal("Staging", active.Name);
    }

    [Fact]
    public async Task JsonFileHistoryRepository_SanitizesSensitiveCredentials()
    {
        var repo = new JsonFileHistoryRepository(_tempDir);

        var request = new ApiRequest
        {
            Name = "Secure Login",
            Url = "https://example.com/login",
            Auth = new AuthSettings
            {
                Type = AuthType.Bearer,
                BearerToken = "super-secret-jwt-token",
                BasicPassword = "secretPassword123"
            },
            Headers = [new KeyValuePairItem("Authorization", "Bearer secret-header-value")]
        };

        var historyItem = new RequestHistoryItem
        {
            Method = "POST",
            Url = request.Url,
            StatusCode = 200,
            RequestSnapshot = request
        };

        await repo.AddAsync(historyItem);
        var retrieved = await repo.GetAllAsync();

        Assert.Single(retrieved);
        var snapshot = retrieved[0].RequestSnapshot;

        // Credentials must be redacted
        Assert.Equal("••••••••", snapshot.Auth.BearerToken);
        Assert.Equal("••••••••", snapshot.Auth.BasicPassword);
        Assert.Equal("••••••••", snapshot.Headers[0].Value);
    }
}
