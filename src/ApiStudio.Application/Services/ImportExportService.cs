using System.Text.Json;
using ApiStudio.Application.Abstractions;
using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;
using ApiStudio.Domain.ValueObjects;

namespace ApiStudio.Application.Services;

public class ImportExportService : IImportExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string ExportCollection(ApiCollection collection)
    {
        return JsonSerializer.Serialize(collection, JsonOptions);
    }

    public ApiCollection ImportCollection(string json)
    {
        var collection = JsonSerializer.Deserialize<ApiCollection>(json, JsonOptions);
        return collection ?? throw new InvalidOperationException("Failed to deserialize collection JSON.");
    }

    public string ExportEnvironment(ApiEnvironment environment)
    {
        return JsonSerializer.Serialize(environment, JsonOptions);
    }

    public ApiEnvironment ImportEnvironment(string json)
    {
        var environment = JsonSerializer.Deserialize<ApiEnvironment>(json, JsonOptions);
        return environment ?? throw new InvalidOperationException("Failed to deserialize environment JSON.");
    }

    public bool IsPostmanCollection(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("info", out var info) && 
                   info.TryGetProperty("schema", out var schema) && 
                   schema.GetString()?.Contains("postman", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }

    public ApiCollection ImportPostmanCollection(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var collection = new ApiCollection
        {
            Name = "Imported Collection"
        };

        if (root.TryGetProperty("info", out var info))
        {
            if (info.TryGetProperty("name", out var nameProp))
            {
                collection.Name = nameProp.GetString() ?? collection.Name;
            }
            if (info.TryGetProperty("description", out var descProp))
            {
                collection.Description = descProp.GetString() ?? string.Empty;
            }
        }

        if (root.TryGetProperty("item", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            ProcessPostmanItems(items, collection.Requests, collection.Folders);
        }

        return collection;
    }

    private static void ProcessPostmanItems(JsonElement items, List<ApiRequest> targetRequests, List<RequestFolder> targetFolders)
    {
        foreach (var item in items.EnumerateArray())
        {
            var itemName = item.TryGetProperty("name", out var np) ? np.GetString() ?? "Request" : "Request";

            // If item has "item" array, it's a folder
            if (item.TryGetProperty("item", out var subItems) && subItems.ValueKind == JsonValueKind.Array)
            {
                var folder = new RequestFolder
                {
                    Name = itemName
                };
                if (item.TryGetProperty("description", out var folderDesc))
                {
                    folder.Description = folderDesc.GetString() ?? "";
                }

                ProcessPostmanItems(subItems, folder.Requests, folder.SubFolders);
                targetFolders.Add(folder);
            }
            else if (item.TryGetProperty("request", out var reqElem))
            {
                var request = ParsePostmanRequest(itemName, reqElem);
                targetRequests.Add(request);
            }
        }
    }

    private static ApiRequest ParsePostmanRequest(string name, JsonElement reqElem)
    {
        var request = new ApiRequest
        {
            Name = name
        };

        // Method
        if (reqElem.TryGetProperty("method", out var methodProp))
        {
            var methodStr = methodProp.GetString()?.ToUpperInvariant() ?? "GET";
            if (Enum.TryParse<HttpMethodType>(methodStr, true, out var method))
            {
                request.Method = method;
            }
            else
            {
                request.Method = HttpMethodType.CUSTOM;
                request.CustomMethod = methodStr;
            }
        }

        // URL
        if (reqElem.TryGetProperty("url", out var urlProp))
        {
            if (urlProp.ValueKind == JsonValueKind.String)
            {
                request.Url = urlProp.GetString() ?? "";
            }
            else if (urlProp.ValueKind == JsonValueKind.Object)
            {
                if (urlProp.TryGetProperty("raw", out var rawUrl))
                {
                    request.Url = rawUrl.GetString() ?? "";
                }

                // Query params
                if (urlProp.TryGetProperty("query", out var queryArray) && queryArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var qp in queryArray.EnumerateArray())
                    {
                        var key = qp.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                        var val = qp.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                        var disabled = qp.TryGetProperty("disabled", out var d) && d.GetBoolean();
                        request.QueryParams.Add(new KeyValuePairItem(key, val, isEnabled: !disabled));
                    }
                }
            }
        }

        // Headers
        if (reqElem.TryGetProperty("header", out var headerArray) && headerArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var h in headerArray.EnumerateArray())
            {
                var key = h.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                var val = h.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                var disabled = h.TryGetProperty("disabled", out var d) && d.GetBoolean();
                request.Headers.Add(new KeyValuePairItem(key, val, isEnabled: !disabled));
            }
        }

        // Body
        if (reqElem.TryGetProperty("body", out var bodyElem) && bodyElem.ValueKind == JsonValueKind.Object)
        {
            var mode = bodyElem.TryGetProperty("mode", out var m) ? m.GetString()?.ToLowerInvariant() : "";
            if (mode == "raw")
            {
                var raw = bodyElem.TryGetProperty("raw", out var r) ? r.GetString() ?? "" : "";
                request.Body.RawContent = raw;
                request.Body.Type = raw.Trim().StartsWith("{") ? BodyType.Json : BodyType.Text;
            }
            else if (mode == "urlencoded" && bodyElem.TryGetProperty("urlencoded", out var urlEncArray) && urlEncArray.ValueKind == JsonValueKind.Array)
            {
                request.Body.Type = BodyType.FormUrlEncoded;
                foreach (var ue in urlEncArray.EnumerateArray())
                {
                    var key = ue.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                    var val = ue.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                    request.Body.UrlEncodedData.Add(new KeyValuePairItem(key, val));
                }
            }
            else if (mode == "formdata" && bodyElem.TryGetProperty("formdata", out var fdArray) && fdArray.ValueKind == JsonValueKind.Array)
            {
                request.Body.Type = BodyType.MultipartFormData;
                foreach (var fd in fdArray.EnumerateArray())
                {
                    var key = fd.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                    var val = fd.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                    request.Body.FormData.Add(new KeyValuePairItem(key, val));
                }
            }
        }

        // Auth
        if (reqElem.TryGetProperty("auth", out var authElem) && authElem.ValueKind == JsonValueKind.Object)
        {
            var authType = authElem.TryGetProperty("type", out var at) ? at.GetString()?.ToLowerInvariant() : "";
            if (authType == "bearer" && authElem.TryGetProperty("bearer", out var bearerArray) && bearerArray.ValueKind == JsonValueKind.Array)
            {
                request.Auth.Type = AuthType.Bearer;
                foreach (var b in bearerArray.EnumerateArray())
                {
                    if (b.TryGetProperty("key", out var bk) && bk.GetString() == "token" && b.TryGetProperty("value", out var bv))
                    {
                        request.Auth.BearerToken = bv.GetString() ?? "";
                    }
                }
            }
            else if (authType == "basic" && authElem.TryGetProperty("basic", out var basicArray) && basicArray.ValueKind == JsonValueKind.Array)
            {
                request.Auth.Type = AuthType.Basic;
                foreach (var b in basicArray.EnumerateArray())
                {
                    if (b.TryGetProperty("key", out var bk))
                    {
                        var keyStr = bk.GetString();
                        if (keyStr == "username" && b.TryGetProperty("value", out var uv))
                            request.Auth.BasicUsername = uv.GetString() ?? "";
                        if (keyStr == "password" && b.TryGetProperty("value", out var pv))
                            request.Auth.BasicPassword = pv.GetString() ?? "";
                    }
                }
            }
        }

        return request;
    }
}
