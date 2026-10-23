using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;
using ApiStudio.Domain.ValueObjects;
using Xunit;

namespace ApiStudio.Domain.Tests;

public class DomainTests
{
    [Fact]
    public void ApiRequest_EffectiveMethod_ReturnsCustomMethod_WhenMethodIsCustom()
    {
        var request = new ApiRequest
        {
            Method = HttpMethodType.CUSTOM,
            CustomMethod = "purge"
        };

        Assert.Equal("PURGE", request.EffectiveMethod);
    }

    [Fact]
    public void ApiRequest_EffectiveMethod_ReturnsStandardMethod_WhenNotCustom()
    {
        var request = new ApiRequest
        {
            Method = HttpMethodType.POST
        };

        Assert.Equal("POST", request.EffectiveMethod);
    }

    [Fact]
    public void ApiRequest_Clone_CreatesDeepIndependentCopy()
    {
        var original = new ApiRequest
        {
            Name = "Original Request",
            Url = "https://example.com/api",
            Method = HttpMethodType.POST,
            Headers = [new KeyValuePairItem("Content-Type", "application/json")],
            QueryParams = [new KeyValuePairItem("page", "1")],
            Auth = new AuthSettings { Type = AuthType.Bearer, BearerToken = "token-123" },
            Body = new RequestBodySettings { Type = BodyType.Json, RawContent = "{\"test\":1}" }
        };

        var clone = original.Clone();

        Assert.NotEqual(original.Id, clone.Id);
        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.Url, clone.Url);
        Assert.Equal(original.Method, clone.Method);
        Assert.Single(clone.Headers);
        Assert.Single(clone.QueryParams);
        Assert.Equal("token-123", clone.Auth.BearerToken);
        Assert.Equal("{\"test\":1}", clone.Body.RawContent);

        // Modifying clone should not affect original
        clone.Headers[0].Value = "text/plain";
        Assert.Equal("application/json", original.Headers[0].Value);
    }

    [Fact]
    public void ApiCollection_Clone_DuplicatesFoldersAndRequests()
    {
        var col = new ApiCollection
        {
            Name = "Master Collection"
        };
        var folder = new RequestFolder { Name = "Auth Folder" };
        folder.Requests.Add(new ApiRequest { Name = "Login" });
        col.Folders.Add(folder);
        col.Requests.Add(new ApiRequest { Name = "Ping" });

        var clone = col.Clone();

        Assert.NotEqual(col.Id, clone.Id);
        Assert.Single(clone.Folders);
        Assert.Single(clone.Requests);
        Assert.Single(clone.Folders[0].Requests);
        Assert.Equal("Login", clone.Folders[0].Requests[0].Name);
    }

    [Fact]
    public void ApiResponse_IsSuccess_TrueFor2xxCodesOnly()
    {
        var success200 = new ApiResponse { StatusCode = 200 };
        var success201 = new ApiResponse { StatusCode = 201 };
        var redirect302 = new ApiResponse { StatusCode = 302 };
        var clientError404 = new ApiResponse { StatusCode = 404 };
        var serverError500 = new ApiResponse { StatusCode = 500 };

        Assert.True(success200.IsSuccess);
        Assert.True(success201.IsSuccess);
        Assert.False(redirect302.IsSuccess);
        Assert.False(clientError404.IsSuccess);
        Assert.False(serverError500.IsSuccess);
    }
}
