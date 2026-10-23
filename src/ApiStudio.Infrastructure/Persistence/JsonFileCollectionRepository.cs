using System.Text.Json;
using ApiStudio.Application.Abstractions;
using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;
using ApiStudio.Domain.ValueObjects;

namespace ApiStudio.Infrastructure.Persistence;

public class JsonFileCollectionRepository : ICollectionRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public JsonFileCollectionRepository(string? dataDirectory = null)
    {
        var dir = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ApiStudio");

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _filePath = Path.Combine(dir, "collections.json");
    }

    public async Task<List<ApiCollection>> GetAllAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                var seedData = CreateSeedCollections();
                await SaveInternalAsync(seedData);
                return seedData;
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var collections = JsonSerializer.Deserialize<List<ApiCollection>>(json, JsonOptions);
            return collections ?? [];
        }
        catch
        {
            return CreateSeedCollections();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAllAsync(IEnumerable<ApiCollection> collections)
    {
        await _semaphore.WaitAsync();
        try
        {
            await SaveInternalAsync(collections.ToList());
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<ApiCollection?> GetByIdAsync(Guid id)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(c => c.Id == id);
    }

    public async Task AddAsync(ApiCollection collection)
    {
        await _semaphore.WaitAsync();
        try
        {
            var list = await ReadInternalAsync();
            list.Add(collection);
            await SaveInternalAsync(list);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateAsync(ApiCollection collection)
    {
        await _semaphore.WaitAsync();
        try
        {
            var list = await ReadInternalAsync();
            var index = list.FindIndex(c => c.Id == collection.Id);
            if (index >= 0)
            {
                list[index] = collection;
            }
            else
            {
                list.Add(collection);
            }
            await SaveInternalAsync(list);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _semaphore.WaitAsync();
        try
        {
            var list = await ReadInternalAsync();
            list.RemoveAll(c => c.Id == id);
            await SaveInternalAsync(list);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<ApiCollection>> ReadInternalAsync()
    {
        if (!File.Exists(_filePath))
        {
            return CreateSeedCollections();
        }

        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<ApiCollection>>(json, JsonOptions) ?? [];
    }

    private async Task SaveInternalAsync(List<ApiCollection> collections)
    {
        var json = JsonSerializer.Serialize(collections, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    private static List<ApiCollection> CreateSeedCollections()
    {
        var sampleCollection = new ApiCollection
        {
            Name = "Sample JSONPlaceholder API",
            Description = "Getting started sample API collection with pre-configured requests and assertions"
        };

        // 1. GET All Posts
        var getPosts = new ApiRequest
        {
            Name = "Get All Posts",
            Description = "Fetches a list of 100 sample posts",
            Method = HttpMethodType.GET,
            Url = "https://jsonplaceholder.typicode.com/posts",
            Headers = [new KeyValuePairItem("Accept", "application/json")]
        };
        getPosts.Assertions.Add(new AssertionRule
        {
            Target = AssertionTarget.StatusCode,
            Operator = AssertionOperator.Equals,
            ExpectedValue = "200"
        });
        sampleCollection.Requests.Add(getPosts);

        // 2. GET Single Post
        var getPost = new ApiRequest
        {
            Name = "Get Post by ID",
            Description = "Fetches post with ID 1",
            Method = HttpMethodType.GET,
            Url = "https://jsonplaceholder.typicode.com/posts/1",
            Headers = [new KeyValuePairItem("Accept", "application/json")]
        };
        getPost.Assertions.Add(new AssertionRule
        {
            Target = AssertionTarget.StatusCode,
            Operator = AssertionOperator.Equals,
            ExpectedValue = "200"
        });
        getPost.Assertions.Add(new AssertionRule
        {
            Target = AssertionTarget.JsonPath,
            PropertyName = "id",
            Operator = AssertionOperator.Equals,
            ExpectedValue = "1"
        });
        sampleCollection.Requests.Add(getPost);

        // 3. POST Create Post
        var createPost = new ApiRequest
        {
            Name = "Create New Post",
            Description = "Creates a new resource with a JSON payload",
            Method = HttpMethodType.POST,
            Url = "https://jsonplaceholder.typicode.com/posts",
            Headers = 
            [
                new KeyValuePairItem("Content-Type", "application/json"),
                new KeyValuePairItem("Accept", "application/json")
            ],
            Body = new RequestBodySettings
            {
                Type = BodyType.Json,
                RawContent = "{\n  \"title\": \"Modern API Testing\",\n  \"body\": \"High performance desktop API client\",\n  \"userId\": 1\n}"
            }
        };
        createPost.Assertions.Add(new AssertionRule
        {
            Target = AssertionTarget.StatusCode,
            Operator = AssertionOperator.Equals,
            ExpectedValue = "201"
        });
        sampleCollection.Requests.Add(createPost);

        // 4. Folder: Users & Comments
        var folder = new RequestFolder
        {
            Name = "Users & Comments",
            Description = "Nested requests folder"
        };
        var getComments = new ApiRequest
        {
            Name = "Get Comments for Post 1",
            Method = HttpMethodType.GET,
            Url = "https://jsonplaceholder.typicode.com/comments",
            QueryParams = [new KeyValuePairItem("postId", "1")]
        };
        getComments.Assertions.Add(new AssertionRule
        {
            Target = AssertionTarget.StatusCode,
            Operator = AssertionOperator.Equals,
            ExpectedValue = "200"
        });
        folder.Requests.Add(getComments);
        sampleCollection.Folders.Add(folder);

        return [sampleCollection];
    }
}
