using ApiStudio.Application.Abstractions;
using ApiStudio.Application.Services;
using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;
using ApiStudio.Presentation.ViewModels;
using Moq;
using Xunit;

namespace ApiStudio.Presentation.Tests;

public class PresentationTests
{
    private readonly Mock<IHttpRequestExecutor> _mockExecutor = new();
    private readonly Mock<IHistoryRepository> _mockHistory = new();
    private readonly VariableResolver _variableResolver = new();
    private readonly AssertionEvaluator _assertionEvaluator = new();

    private RequestTabViewModel CreateTestTab(Dictionary<string, string>? variables = null)
    {
        var vars = variables ?? new Dictionary<string, string>();
        return new RequestTabViewModel(
            _mockExecutor.Object,
            _variableResolver,
            _assertionEvaluator,
            _mockHistory.Object,
            () => vars);
    }

    [Fact]
    public void RequestTabViewModel_SyncsQueryParamsFromUrl()
    {
        var tab = CreateTestTab();

        tab.Url = "https://example.com/search?q=postman&page=2";

        Assert.Equal(2, tab.QueryParams.Count);
        Assert.Equal("q", tab.QueryParams[0].Key);
        Assert.Equal("postman", tab.QueryParams[0].Value);
        Assert.Equal("page", tab.QueryParams[1].Key);
        Assert.Equal("2", tab.QueryParams[1].Value);
    }

    [Fact]
    public void RequestTabViewModel_SyncsUrlFromQueryParams()
    {
        var tab = CreateTestTab();
        tab.Url = "https://example.com/api";

        tab.AddQueryParamCommand.Execute(null);
        tab.QueryParams[0].Key = "limit";
        tab.QueryParams[0].Value = "10";

        Assert.Equal("https://example.com/api?limit=10", tab.Url);
    }

    [Fact]
    public void RequestTabViewModel_FormatJson_IndentsValidJson()
    {
        var tab = CreateTestTab();
        tab.RawBodyContent = "{\"title\":\"Test\",\"active\":true}";

        tab.FormatJsonCommand.Execute(null);

        Assert.Contains("\n", tab.RawBodyContent);
        Assert.Contains("  \"title\": \"Test\"", tab.RawBodyContent);
    }

    [Fact]
    public void ResponseViewModel_FormatBodyContent_BeautifiesJson()
    {
        var vm = new ResponseViewModel();
        var unformatted = "{\"id\":1,\"name\":\"ApiStudio\"}";

        vm.Body = unformatted;

        Assert.Contains("\n", vm.FormattedBody);
        Assert.Contains("  \"id\": 1", vm.FormattedBody);
    }

    [Fact]
    public void ResponseViewModel_FormatBodyContent_BeautifiesXml()
    {
        var vm = new ResponseViewModel();
        var unformatted = "<root><item id=\"1\">Value</item></root>";

        vm.Body = unformatted;

        Assert.Contains("\n", vm.FormattedBody);
        Assert.Contains("  <item", vm.FormattedBody);
    }

    [Fact]
    public void ResponseViewModel_Reset_ClearsAllData()
    {
        var vm = new ResponseViewModel();
        vm.LoadFromResponse(new ApiResponse
        {
            StatusCode = 200,
            StatusDescription = "OK",
            ElapsedMilliseconds = 50,
            Body = "{\"test\":1}"
        });

        Assert.True(vm.HasResponse);
        Assert.Equal(200, vm.StatusCode);

        vm.Reset();

        Assert.False(vm.HasResponse);
        Assert.Equal(0, vm.StatusCode);
        Assert.Empty(vm.Body);
    }

    [Fact]
    public async Task RequestTabViewModel_ExecuteRequest_FiresHistoryItemAddedEvent()
    {
        _mockExecutor.Setup(e => e.ExecuteAsync(It.IsAny<ApiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse
            {
                StatusCode = 200,
                StatusDescription = "OK",
                ElapsedMilliseconds = 45,
                Body = "{\"success\":true}"
            });

        var tab = CreateTestTab();
        tab.Url = "https://example.com/api";

        RequestHistoryItem? loggedItem = null;
        tab.HistoryItemAdded += item => loggedItem = item;

        await tab.ExecuteRequestAsync();

        Assert.NotNull(loggedItem);
        Assert.Equal("https://example.com/api", loggedItem.Url);
        Assert.Equal(200, loggedItem.StatusCode);
    }

    [Fact]
    public void SettingsViewModel_ChangingTheme_AppliesThemeCallback()
    {
        var mockPrefs = new Mock<IUserPreferencesRepository>();
        mockPrefs.Setup(p => p.GetAsync()).ReturnsAsync(new UserPreferences { Theme = "Light" });

        bool themeChangedToDark = false;
        var vm = new SettingsViewModel(
            mockPrefs.Object,
            _mockHistory.Object,
            applyTheme: isDark => themeChangedToDark = isDark,
            onHistoryCleared: () => { },
            currentHistoryCount: 5,
            isCurrentDark: false);

        vm.SelectedTheme = "Dark";

        Assert.True(themeChangedToDark);
    }
}
