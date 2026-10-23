using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using ApiStudio.Application.Abstractions;
using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;
using ApiStudio.Domain.ValueObjects;
using ApiStudio.Presentation.Common;
using Microsoft.Win32;

namespace ApiStudio.Presentation.ViewModels;

public class RequestTabViewModel : ObservableObject
{
    private readonly IHttpRequestExecutor _requestExecutor;
    private readonly IVariableResolver _variableResolver;
    private readonly IAssertionEvaluator _assertionEvaluator;
    private readonly IHistoryRepository _historyRepository;
    private readonly Func<IDictionary<string, string>> _getActiveVariables;

    private Guid _id;
    private string _name = "New Request";
    private HttpMethodType _method = HttpMethodType.GET;
    private string _customMethod = string.Empty;
    private string _url = "https://";
    private bool _isDirty;
    private bool _isRunning;
    private int _timeoutSeconds = 30;
    private bool _followRedirects = true;
    private CancellationTokenSource? _cts;
    private bool _isSyncing;

    // Body Settings
    private BodyType _selectedBodyType = BodyType.None;
    private string _rawBodyContent = string.Empty;
    private string _binaryFilePath = string.Empty;

    // Auth Settings
    private AuthType _selectedAuthType = AuthType.None;
    private string _bearerToken = string.Empty;
    private string _basicUsername = string.Empty;
    private string _basicPassword = string.Empty;
    private string _apiKeyName = string.Empty;
    private string _apiKeyValue = string.Empty;
    private ApiKeyLocation _apiKeyLocation = ApiKeyLocation.Header;

    public Guid Id => _id;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                IsDirty = true;
            }
        }
    }

    public HttpMethodType Method
    {
        get => _method;
        set
        {
            if (SetProperty(ref _method, value))
            {
                OnPropertyChanged(nameof(MethodString));
                IsDirty = true;
            }
        }
    }

    public string MethodString => Method == HttpMethodType.CUSTOM 
        ? (string.IsNullOrWhiteSpace(CustomMethod) ? "CUSTOM" : CustomMethod.ToUpperInvariant())
        : Method.ToString();

    public string CustomMethod
    {
        get => _customMethod;
        set
        {
            if (SetProperty(ref _customMethod, value))
            {
                OnPropertyChanged(nameof(MethodString));
                IsDirty = true;
            }
        }
    }

    public string Url
    {
        get => _url;
        set
        {
            if (SetProperty(ref _url, value))
            {
                IsDirty = true;
                if (!_isSyncing)
                {
                    SyncQueryParamsFromUrl(value);
                }
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public int TimeoutSeconds
    {
        get => _timeoutSeconds;
        set => SetProperty(ref _timeoutSeconds, value);
    }

    public bool FollowRedirects
    {
        get => _followRedirects;
        set => SetProperty(ref _followRedirects, value);
    }

    // Body
    public BodyType SelectedBodyType
    {
        get => _selectedBodyType;
        set
        {
            if (SetProperty(ref _selectedBodyType, value))
            {
                IsDirty = true;
            }
        }
    }

    public string RawBodyContent
    {
        get => _rawBodyContent;
        set
        {
            if (SetProperty(ref _rawBodyContent, value))
            {
                IsDirty = true;
            }
        }
    }

    public string BinaryFilePath
    {
        get => _binaryFilePath;
        set
        {
            if (SetProperty(ref _binaryFilePath, value))
            {
                IsDirty = true;
            }
        }
    }

    // Auth
    public AuthType SelectedAuthType
    {
        get => _selectedAuthType;
        set
        {
            if (SetProperty(ref _selectedAuthType, value))
            {
                IsDirty = true;
            }
        }
    }

    public string BearerToken
    {
        get => _bearerToken;
        set
        {
            if (SetProperty(ref _bearerToken, value))
            {
                IsDirty = true;
            }
        }
    }

    public string BasicUsername
    {
        get => _basicUsername;
        set
        {
            if (SetProperty(ref _basicUsername, value))
            {
                IsDirty = true;
            }
        }
    }

    public string BasicPassword
    {
        get => _basicPassword;
        set
        {
            if (SetProperty(ref _basicPassword, value))
            {
                IsDirty = true;
            }
        }
    }

    public string ApiKeyName
    {
        get => _apiKeyName;
        set
        {
            if (SetProperty(ref _apiKeyName, value))
            {
                IsDirty = true;
            }
        }
    }

    public string ApiKeyValue
    {
        get => _apiKeyValue;
        set
        {
            if (SetProperty(ref _apiKeyValue, value))
            {
                IsDirty = true;
            }
        }
    }

    public ApiKeyLocation ApiKeyLocation
    {
        get => _apiKeyLocation;
        set
        {
            if (SetProperty(ref _apiKeyLocation, value))
            {
                IsDirty = true;
            }
        }
    }

    public ObservableCollection<KeyValuePairViewModel> QueryParams { get; } = [];
    public ObservableCollection<KeyValuePairViewModel> Headers { get; } = [];
    public ObservableCollection<KeyValuePairViewModel> UrlEncodedParams { get; } = [];
    public ObservableCollection<KeyValuePairViewModel> FormDataParams { get; } = [];
    public ObservableCollection<AssertionItemViewModel> Assertions { get; } = [];

    public ResponseViewModel Response { get; } = new();

    public ICommand SendCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand FormatJsonCommand { get; }
    public ICommand SelectFileCommand { get; }

    public ICommand AddQueryParamCommand { get; }
    public ICommand AddHeaderCommand { get; }
    public ICommand AddUrlEncodedParamCommand { get; }
    public ICommand AddFormDataParamCommand { get; }
    public ICommand AddAssertionCommand { get; }

    public event Action<RequestTabViewModel>? SaveRequested;

    public RequestTabViewModel(
        IHttpRequestExecutor requestExecutor,
        IVariableResolver variableResolver,
        IAssertionEvaluator assertionEvaluator,
        IHistoryRepository historyRepository,
        Func<IDictionary<string, string>> getActiveVariables)
    {
        _requestExecutor = requestExecutor;
        _variableResolver = variableResolver;
        _assertionEvaluator = assertionEvaluator;
        _historyRepository = historyRepository;
        _getActiveVariables = getActiveVariables;
        _id = Guid.NewGuid();

        SendCommand = new AsyncRelayCommand(ExecuteRequestAsync, () => !IsRunning);
        CancelCommand = new RelayCommand(CancelRequest, () => IsRunning);
        SaveCommand = new RelayCommand(() => SaveRequested?.Invoke(this));
        FormatJsonCommand = new RelayCommand(FormatJson);
        SelectFileCommand = new RelayCommand(SelectBinaryFile);

        AddQueryParamCommand = new RelayCommand(AddQueryParam);
        AddHeaderCommand = new RelayCommand(AddHeader);
        AddUrlEncodedParamCommand = new RelayCommand(AddUrlEncodedParam);
        AddFormDataParamCommand = new RelayCommand(AddFormDataParam);
        AddAssertionCommand = new RelayCommand(AddAssertion);

        // Pre-populate with typical Accept header
        AddHeaderItem("Accept", "*/*");
    }

    public void LoadFromModel(ApiRequest request)
    {
        _id = request.Id;
        _name = request.Name;
        _method = request.Method;
        _customMethod = request.CustomMethod;
        _url = request.Url;
        _timeoutSeconds = request.TimeoutSeconds;
        _followRedirects = request.FollowRedirects;

        // Auth
        _selectedAuthType = request.Auth.Type;
        _bearerToken = request.Auth.BearerToken;
        _basicUsername = request.Auth.BasicUsername;
        _basicPassword = request.Auth.BasicPassword;
        _apiKeyName = request.Auth.ApiKeyName;
        _apiKeyValue = request.Auth.ApiKeyValue;
        _apiKeyLocation = request.Auth.ApiKeyLocation;

        // Body
        _selectedBodyType = request.Body.Type;
        _rawBodyContent = request.Body.RawContent;
        _binaryFilePath = request.Body.BinaryFilePath;

        QueryParams.Clear();
        foreach (var qp in request.QueryParams)
        {
            AddQueryParamItem(qp.Key, qp.Value, qp.Description, qp.IsEnabled);
        }

        Headers.Clear();
        foreach (var h in request.Headers)
        {
            AddHeaderItem(h.Key, h.Value, h.Description, h.IsEnabled);
        }

        UrlEncodedParams.Clear();
        foreach (var ue in request.Body.UrlEncodedData)
        {
            AddUrlEncodedItem(ue.Key, ue.Value, ue.Description, ue.IsEnabled);
        }

        FormDataParams.Clear();
        foreach (var fd in request.Body.FormData)
        {
            AddFormDataItem(fd.Key, fd.Value, fd.Description, fd.IsEnabled);
        }

        Assertions.Clear();
        foreach (var a in request.Assertions)
        {
            AddAssertionItem(a);
        }

        OnPropertyChanged(string.Empty);
        IsDirty = false;
    }

    public ApiRequest ToModel()
    {
        return new ApiRequest
        {
            Id = _id,
            Name = _name,
            Method = _method,
            CustomMethod = _customMethod,
            Url = _url,
            TimeoutSeconds = _timeoutSeconds,
            FollowRedirects = _followRedirects,
            Auth = new AuthSettings
            {
                Type = _selectedAuthType,
                BearerToken = _bearerToken,
                BasicUsername = _basicUsername,
                BasicPassword = _basicPassword,
                ApiKeyName = _apiKeyName,
                ApiKeyValue = _apiKeyValue,
                ApiKeyLocation = _apiKeyLocation
            },
            Body = new RequestBodySettings
            {
                Type = _selectedBodyType,
                RawContent = _rawBodyContent,
                BinaryFilePath = _binaryFilePath,
                UrlEncodedData = UrlEncodedParams.Select(x => x.ToModel()).ToList(),
                FormData = FormDataParams.Select(x => x.ToModel()).ToList()
            },
            QueryParams = QueryParams.Select(x => x.ToModel()).ToList(),
            Headers = Headers.Select(x => x.ToModel()).ToList(),
            Assertions = Assertions.Select(a => new AssertionRule
            {
                Id = a.Id,
                Target = a.Target,
                PropertyName = a.PropertyName,
                Operator = a.Operator,
                ExpectedValue = a.ExpectedValue,
                IsEnabled = a.IsEnabled
            }).ToList()
        };
    }

    public async Task ExecuteRequestAsync()
    {
        if (IsRunning) return;

        IsRunning = true;
        _cts = new CancellationTokenSource();
        Response.Reset();

        try
        {
            var model = ToModel();
            var variables = _getActiveVariables();
            var resolvedRequest = _variableResolver.ResolveRequest(model, variables);

            var apiResponse = await _requestExecutor.ExecuteAsync(resolvedRequest, _cts.Token);

            // Evaluate Assertions
            if (model.Assertions.Count > 0)
            {
                apiResponse.TestResults = _assertionEvaluator.Evaluate(apiResponse, model.Assertions);
            }

            Response.LoadFromResponse(apiResponse);

            // Log to History
            await _historyRepository.AddAsync(new RequestHistoryItem
            {
                ExecutedAt = DateTime.UtcNow,
                Method = model.EffectiveMethod,
                Url = resolvedRequest.Url,
                StatusCode = apiResponse.StatusCode > 0 ? apiResponse.StatusCode : null,
                StatusDescription = apiResponse.StatusDescription,
                ElapsedMilliseconds = apiResponse.ElapsedMilliseconds,
                RequestSnapshot = model
            });
        }
        catch (Exception ex)
        {
            Response.LoadFromResponse(ApiResponse.Failure($"Error: {ex.Message}"));
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void CancelRequest()
    {
        _cts?.Cancel();
    }

    private void FormatJson()
    {
        if (string.IsNullOrWhiteSpace(RawBodyContent)) return;

        try
        {
            using var doc = JsonDocument.Parse(RawBodyContent);
            RawBodyContent = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            // Invalid JSON, leave as is
        }
    }

    private void SelectBinaryFile()
    {
        var openDialog = new OpenFileDialog
        {
            Title = "Select File to Upload",
            Filter = "All files (*.*)|*.*"
        };

        if (openDialog.ShowDialog() == true)
        {
            BinaryFilePath = openDialog.FileName;
        }
    }

    // 2-Way URL <-> QueryParams Sync
    private void SyncQueryParamsFromUrl(string url)
    {
        if (_isSyncing) return;
        _isSyncing = true;

        try
        {
            QueryParams.Clear();
            var qIdx = url.IndexOf('?');
            if (qIdx >= 0 && qIdx < url.Length - 1)
            {
                var queryStr = url[(qIdx + 1)..];
                var pairs = queryStr.Split('&', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var eqIdx = pair.IndexOf('=');
                    if (eqIdx >= 0)
                    {
                        var k = Uri.UnescapeDataString(pair[..eqIdx]);
                        var v = Uri.UnescapeDataString(pair[(eqIdx + 1)..]);
                        AddQueryParamItem(k, v);
                    }
                    else
                    {
                        AddQueryParamItem(Uri.UnescapeDataString(pair), "");
                    }
                }
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void SyncUrlFromQueryParams()
    {
        if (_isSyncing) return;
        _isSyncing = true;

        try
        {
            var baseUrl = Url;
            var qIdx = baseUrl.IndexOf('?');
            if (qIdx >= 0)
            {
                baseUrl = baseUrl[..qIdx];
            }

            var enabledParams = QueryParams.Where(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (enabledParams.Count == 0)
            {
                Url = baseUrl;
                return;
            }

            var sb = new StringBuilder(baseUrl);
            sb.Append('?');
            for (int i = 0; i < enabledParams.Count; i++)
            {
                if (i > 0) sb.Append('&');
                sb.Append(Uri.EscapeDataString(enabledParams[i].Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(enabledParams[i].Value));
            }

            Url = sb.ToString();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    // Item Helpers
    private void AddQueryParam()
    {
        AddQueryParamItem("", "");
    }

    private void AddQueryParamItem(string key, string val, string desc = "", bool enabled = true)
    {
        var vm = new KeyValuePairViewModel(key, val, desc, enabled);
        vm.Changed += () =>
        {
            IsDirty = true;
            SyncUrlFromQueryParams();
        };
        vm.DeleteCommand = new RelayCommand(() =>
        {
            QueryParams.Remove(vm);
            IsDirty = true;
            SyncUrlFromQueryParams();
        });
        QueryParams.Add(vm);
    }

    private void AddHeader()
    {
        AddHeaderItem("", "");
    }

    private void AddHeaderItem(string key, string val, string desc = "", bool enabled = true)
    {
        var vm = new KeyValuePairViewModel(key, val, desc, enabled);
        vm.Changed += () => IsDirty = true;
        vm.DeleteCommand = new RelayCommand(() =>
        {
            Headers.Remove(vm);
            IsDirty = true;
        });
        Headers.Add(vm);
    }

    private void AddUrlEncodedParam()
    {
        AddUrlEncodedItem("", "");
    }

    private void AddUrlEncodedItem(string key, string val, string desc = "", bool enabled = true)
    {
        var vm = new KeyValuePairViewModel(key, val, desc, enabled);
        vm.Changed += () => IsDirty = true;
        vm.DeleteCommand = new RelayCommand(() =>
        {
            UrlEncodedParams.Remove(vm);
            IsDirty = true;
        });
        UrlEncodedParams.Add(vm);
    }

    private void AddFormDataParam()
    {
        AddFormDataItem("", "");
    }

    private void AddFormDataItem(string key, string val, string desc = "", bool enabled = true)
    {
        var vm = new KeyValuePairViewModel(key, val, desc, enabled);
        vm.Changed += () => IsDirty = true;
        vm.DeleteCommand = new RelayCommand(() =>
        {
            FormDataParams.Remove(vm);
            IsDirty = true;
        });
        FormDataParams.Add(vm);
    }

    private void AddAssertion()
    {
        AddAssertionItem(new AssertionRule());
    }

    private void AddAssertionItem(AssertionRule rule)
    {
        var vm = new AssertionItemViewModel
        {
            Id = rule.Id,
            Target = rule.Target,
            PropertyName = rule.PropertyName,
            Operator = rule.Operator,
            ExpectedValue = rule.ExpectedValue,
            IsEnabled = rule.IsEnabled
        };
        vm.DeleteCommand = new RelayCommand(() =>
        {
            Assertions.Remove(vm);
            IsDirty = true;
        });
        Assertions.Add(vm);
        IsDirty = true;
    }
}
