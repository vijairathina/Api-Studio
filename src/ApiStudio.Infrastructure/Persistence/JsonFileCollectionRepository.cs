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
        // 1. User API
        var userCol = new ApiCollection
        {
            Name = "User API",
            Description = "User profile and authentication services"
        };
        var getUser = new ApiRequest
        {
            Name = "/users",
            Description = "Fetches active user list and profiles",
            Method = HttpMethodType.GET,
            Url = "https://api.example.com/v1/users/active",
            Headers = [new KeyValuePairItem("Accept", "application/json")]
        };
        getUser.Assertions.Add(new AssertionRule
        {
            Target = AssertionTarget.StatusCode,
            Operator = AssertionOperator.Equals,
            ExpectedValue = "200"
        });
        userCol.Requests.Add(getUser);

        var registerUser = new ApiRequest
        {
            Name = "/users/register",
            Description = "Registers a new user account",
            Method = HttpMethodType.POST,
            Url = "https://api.example.com/v1/users/register",
            Headers = 
            [
                new KeyValuePairItem("Content-Type", "application/json"),
                new KeyValuePairItem("Accept", "application/json")
            ],
            Body = new RequestBodySettings
            {
                Type = BodyType.Json,
                RawContent = "{\n  \"username\": \"alice_johnson\",\n  \"email\": \"alice.j@example.com\",\n  \"role\": \"admin\"\n}"
            }
        };
        registerUser.Assertions.Add(new AssertionRule
        {
            Target = AssertionTarget.StatusCode,
            Operator = AssertionOperator.Equals,
            ExpectedValue = "201"
        });
        userCol.Requests.Add(registerUser);

        // 2. Product Catalog
        var productCol = new ApiCollection
        {
            Name = "Product Catalog",
            Description = "Product catalog and inventory queries"
        };
        var getProducts = new ApiRequest
        {
            Name = "/products",
            Description = "List all active catalog products",
            Method = HttpMethodType.GET,
            Url = "https://api.example.com/v1/products"
        };
        productCol.Requests.Add(getProducts);

        var getProductById = new ApiRequest
        {
            Name = "/products/{id}",
            Description = "Get single product by item ID",
            Method = HttpMethodType.GET,
            Url = "https://api.example.com/v1/products/{id}",
            QueryParams = [new KeyValuePairItem("id", "101")]
        };
        productCol.Requests.Add(getProductById);

        // 3. Order Management
        var orderCol = new ApiCollection
        {
            Name = "Order Management",
            Description = "Customer order processing and cart checkout"
        };
        var updateCart = new ApiRequest
        {
            Name = "Update Cart",
            Method = HttpMethodType.PUT,
            Url = "https://api.example.com/v1/cart"
        };
        orderCol.Requests.Add(updateCart);

        var deleteOrder = new ApiRequest
        {
            Name = "Delete Order",
            Method = HttpMethodType.DELETE,
            Url = "https://api.example.com/v1/orders/402"
        };
        orderCol.Requests.Add(deleteOrder);

        // 4. Authentication
        var authCol = new ApiCollection
        {
            Name = "Authentication",
            Description = "OAuth2 and API authentication tokens"
        };
        var loginReq = new ApiRequest
        {
            Name = "/auth/login",
            Method = HttpMethodType.POST,
            Url = "https://api.example.com/v1/auth/login"
        };
        authCol.Requests.Add(loginReq);

        return [userCol, productCol, orderCol, authCol];
    }
}
