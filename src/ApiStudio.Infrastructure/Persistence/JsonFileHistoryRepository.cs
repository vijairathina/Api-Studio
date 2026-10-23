using System.Text.Json;
using ApiStudio.Application.Abstractions;
using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;

namespace ApiStudio.Infrastructure.Persistence;

public class JsonFileHistoryRepository : IHistoryRepository
{
    private const int MaxHistoryCount = 100;
    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public JsonFileHistoryRepository(string? dataDirectory = null)
    {
        var dir = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ApiStudio");

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _filePath = Path.Combine(dir, "history.json");
    }

    public async Task<List<RequestHistoryItem>> GetAllAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var history = JsonSerializer.Deserialize<List<RequestHistoryItem>>(json, JsonOptions);
            return history?.OrderByDescending(h => h.ExecutedAt).ToList() ?? [];
        }
        catch
        {
            return [];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task AddAsync(RequestHistoryItem item)
    {
        await _semaphore.WaitAsync();
        try
        {
            var history = new List<RequestHistoryItem>();
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath);
                history = JsonSerializer.Deserialize<List<RequestHistoryItem>>(json, JsonOptions) ?? [];
            }

            // Sanitize sensitive credentials from snapshot
            var sanitizedSnapshot = item.RequestSnapshot.Clone();
            SanitizeSnapshot(sanitizedSnapshot);
            item.RequestSnapshot = sanitizedSnapshot;

            history.Insert(0, item);

            // Cap to maximum count
            if (history.Count > MaxHistoryCount)
            {
                history = history.Take(MaxHistoryCount).ToList();
            }

            var updatedJson = JsonSerializer.Serialize(history, JsonOptions);
            await File.WriteAllTextAsync(_filePath, updatedJson);
        }
        catch
        {
            // Logging or non-fatal fallback
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static void SanitizeSnapshot(ApiRequest request)
    {
        if (request.Auth != null)
        {
            if (!string.IsNullOrEmpty(request.Auth.BearerToken))
                request.Auth.BearerToken = "••••••••";
            if (!string.IsNullOrEmpty(request.Auth.BasicPassword))
                request.Auth.BasicPassword = "••••••••";
            if (!string.IsNullOrEmpty(request.Auth.ApiKeyValue))
                request.Auth.ApiKeyValue = "••••••••";
        }

        foreach (var header in request.Headers)
        {
            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header.Key, "X-Api-Key", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header.Key, "ApiKey", StringComparison.OrdinalIgnoreCase))
            {
                header.Value = "••••••••";
            }
        }
    }
}
