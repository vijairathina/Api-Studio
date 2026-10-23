using ApiStudio.Application.Abstractions;
using ApiStudio.Application.Services;
using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;
using ApiStudio.Domain.ValueObjects;
using Moq;
using Xunit;

namespace ApiStudio.Application.Tests;

public class ApplicationTests
{
    private readonly VariableResolver _variableResolver = new();
    private readonly AssertionEvaluator _assertionEvaluator = new();
    private readonly CurlConverter _curlConverter = new();
    private readonly ImportExportService _importExportService = new();

    [Fact]
    public void VariableResolver_ResolvesNestedVariablesInUrlAndHeaders()
    {
        var variables = new Dictionary<string, string>
        {
            { "baseUrl", "https://api.example.com" },
            { "version", "v1" },
            { "apiKey", "secret-key-123" }
        };

        var request = new ApiRequest
        {
            Url = "{{baseUrl}}/{{version}}/users",
            Headers = [new KeyValuePairItem("X-Api-Key", "{{apiKey}}")],
            Body = new RequestBodySettings
            {
                Type = BodyType.Json,
                RawContent = "{\"endpoint\": \"{{baseUrl}}\"}"
            }
        };

        var resolved = _variableResolver.ResolveRequest(request, variables);

        Assert.Equal("https://api.example.com/v1/users", resolved.Url);
        Assert.Equal("secret-key-123", resolved.Headers[0].Value);
        Assert.Contains("https://api.example.com", resolved.Body.RawContent);
    }

    [Fact]
    public void VariableResolver_PreservesMissingVariablesWithoutCrash()
    {
        var variables = new Dictionary<string, string>
        {
            { "knownVar", "test" }
        };

        var input = "https://example.com/{{missingVar}}/{{knownVar}}";
        var result = _variableResolver.Resolve(input, variables);

        Assert.Equal("https://example.com/{{missingVar}}/test", result);
    }

    [Fact]
    public void AssertionEvaluator_ValidatesStatusCodeAndJsonPath()
    {
        var response = new ApiResponse
        {
            StatusCode = 200,
            ElapsedMilliseconds = 150,
            Body = "{\"user\": {\"id\": 42, \"name\": \"Antigravity\"}}",
            Headers = [new KeyValuePair<string, string>("Content-Type", "application/json")]
        };

        var rules = new List<AssertionRule>
        {
            new()
            {
                Target = AssertionTarget.StatusCode,
                Operator = AssertionOperator.Equals,
                ExpectedValue = "200"
            },
            new()
            {
                Target = AssertionTarget.ResponseTimeMs,
                Operator = AssertionOperator.LessThan,
                ExpectedValue = "300"
            },
            new()
            {
                Target = AssertionTarget.JsonPath,
                PropertyName = "$.user.id",
                Operator = AssertionOperator.Equals,
                ExpectedValue = "42"
            },
            new()
            {
                Target = AssertionTarget.Header,
                PropertyName = "Content-Type",
                Operator = AssertionOperator.Contains,
                ExpectedValue = "json"
            }
        };

        var results = _assertionEvaluator.Evaluate(response, rules);

        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.True(r.Passed));
    }

    [Fact]
    public void AssertionEvaluator_DetectsFailedAssertion()
    {
        var response = new ApiResponse
        {
            StatusCode = 404,
            Body = "Not Found"
        };

        var rules = new List<AssertionRule>
        {
            new()
            {
                Target = AssertionTarget.StatusCode,
                Operator = AssertionOperator.Equals,
                ExpectedValue = "200"
            }
        };

        var results = _assertionEvaluator.Evaluate(response, rules);

        Assert.Single(results);
        Assert.False(results[0].Passed);
        Assert.Equal("404", results[0].ActualValue);
    }

    [Fact]
    public void CurlConverter_ExportsAndParses_CurlCommands()
    {
        var original = new ApiRequest
        {
            Method = HttpMethodType.POST,
            Url = "https://example.com/api/items",
            Headers = [new KeyValuePairItem("Accept", "application/json")],
            Auth = new AuthSettings { Type = AuthType.Bearer, BearerToken = "token_abc" },
            Body = new RequestBodySettings
            {
                Type = BodyType.Json,
                RawContent = "{\"name\":\"Sample\"}"
            }
        };

        var curl = _curlConverter.ExportCurl(original);

        Assert.Contains("curl", curl);
        Assert.Contains("--request POST", curl);
        Assert.Contains("https://example.com/api/items", curl);
        Assert.Contains("Authorization: Bearer token_abc", curl);
        Assert.Contains("{\"name\":\"Sample\"}", curl);

        // Test Parse
        var parsed = _curlConverter.ParseCurl(curl);
        Assert.Equal(HttpMethodType.POST, parsed.Method);
        Assert.Equal("https://example.com/api/items", parsed.Url);
        Assert.Equal(AuthType.Bearer, parsed.Auth.Type);
        Assert.Equal("token_abc", parsed.Auth.BearerToken);
    }

    [Fact]
    public void ImportExportService_ImportsPostmanCollectionV2()
    {
        var postmanJson = """
        {
          "info": {
            "name": "Sample Postman V2 Collection",
            "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
          },
          "item": [
            {
              "name": "Get Users",
              "request": {
                "method": "GET",
                "header": [
                  { "key": "Accept", "value": "application/json" }
                ],
                "url": {
                  "raw": "https://jsonplaceholder.typicode.com/users?page=1",
                  "query": [
                    { "key": "page", "value": "1" }
                  ]
                }
              }
            }
          ]
        }
        """;

        Assert.True(_importExportService.IsPostmanCollection(postmanJson));

        var collection = _importExportService.ImportPostmanCollection(postmanJson);

        Assert.Equal("Sample Postman V2 Collection", collection.Name);
        Assert.Single(collection.Requests);
        Assert.Equal("Get Users", collection.Requests[0].Name);
        Assert.Equal(HttpMethodType.GET, collection.Requests[0].Method);
        Assert.Equal("https://jsonplaceholder.typicode.com/users?page=1", collection.Requests[0].Url);
        Assert.Single(collection.Requests[0].Headers);
        Assert.Single(collection.Requests[0].QueryParams);
    }

    [Fact]
    public async Task CollectionRunner_ExecutesAllRequestsAndAggregatesResults()
    {
        var mockExecutor = new Mock<IHttpRequestExecutor>();
        mockExecutor.Setup(e => e.ExecuteAsync(It.IsAny<ApiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse
            {
                StatusCode = 200,
                StatusDescription = "OK",
                ElapsedMilliseconds = 100,
                Body = "{\"status\":\"ok\"}"
            });

        var runner = new CollectionRunner(mockExecutor.Object, _variableResolver, _assertionEvaluator);

        var col = new ApiCollection { Name = "Runner Test" };
        var req1 = new ApiRequest { Name = "Req 1", Url = "https://test.com/1" };
        req1.Assertions.Add(new AssertionRule { Target = AssertionTarget.StatusCode, ExpectedValue = "200" });
        var req2 = new ApiRequest { Name = "Req 2", Url = "https://test.com/2" };
        req2.Assertions.Add(new AssertionRule { Target = AssertionTarget.StatusCode, ExpectedValue = "200" });
        col.Requests.Add(req1);
        col.Requests.Add(req2);

        var result = await runner.RunAsync(col, new Dictionary<string, string>());

        Assert.Equal(2, result.TotalRequests);
        Assert.Equal(2, result.PassedRequests);
        Assert.Equal(0, result.FailedRequests);
    }
}
