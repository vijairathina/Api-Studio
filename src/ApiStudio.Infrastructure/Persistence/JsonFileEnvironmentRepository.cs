using System.Text.Json;
using ApiStudio.Application.Abstractions;
using ApiStudio.Domain.Entities;

namespace ApiStudio.Infrastructure.Persistence;

public class JsonFileEnvironmentRepository : IEnvironmentRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public JsonFileEnvironmentRepository(string? dataDirectory = null)
    {
        var dir = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ApiStudio");

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _filePath = Path.Combine(dir, "environments.json");
    }

    public async Task<List<ApiEnvironment>> GetAllAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                var seedData = CreateSeedEnvironments();
                await SaveInternalAsync(seedData);
                return seedData;
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var environments = JsonSerializer.Deserialize<List<ApiEnvironment>>(json, JsonOptions);
            return environments ?? [];
        }
        catch
        {
            return CreateSeedEnvironments();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAllAsync(IEnumerable<ApiEnvironment> environments)
    {
        await _semaphore.WaitAsync();
        try
        {
            await SaveInternalAsync(environments.ToList());
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<ApiEnvironment?> GetActiveAsync()
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(e => e.IsActive);
    }

    public async Task SetActiveAsync(Guid? id)
    {
        var all = await GetAllAsync();
        foreach (var env in all)
        {
            env.IsActive = (id.HasValue && env.Id == id.Value);
        }
        await SaveAllAsync(all);
    }

    private async Task SaveInternalAsync(List<ApiEnvironment> list)
    {
        var json = JsonSerializer.Serialize(list, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    private static List<ApiEnvironment> CreateSeedEnvironments()
    {
        return
        [
            new ApiEnvironment
            {
                Name = "Development",
                IsActive = true,
                Variables =
                [
                    new EnvironmentVariable("baseUrl", "https://jsonplaceholder.typicode.com"),
                    new EnvironmentVariable("apiKey", "dev_secret_key_12345", isSecret: true)
                ]
            },
            new ApiEnvironment
            {
                Name = "Staging",
                IsActive = false,
                Variables =
                [
                    new EnvironmentVariable("baseUrl", "https://staging.api.example.com"),
                    new EnvironmentVariable("apiKey", "stage_secret_key_67890", isSecret: true)
                ]
            },
            new ApiEnvironment
            {
                Name = "Production",
                IsActive = false,
                Variables =
                [
                    new EnvironmentVariable("baseUrl", "https://api.example.com"),
                    new EnvironmentVariable("apiKey", "prod_secret_key_99999", isSecret: true)
                ]
            }
        ];
    }
}
