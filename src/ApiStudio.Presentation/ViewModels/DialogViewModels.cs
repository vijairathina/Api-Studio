using System.Collections.ObjectModel;
using System.Windows.Input;
using ApiStudio.Application.Abstractions;
using ApiStudio.Domain.Entities;
using ApiStudio.Presentation.Common;

namespace ApiStudio.Presentation.ViewModels;

public class CollectionRunnerViewModel : ObservableObject
{
    private readonly ICollectionRunner _collectionRunner;
    private readonly Func<IDictionary<string, string>> _getActiveVariables;

    private ApiCollection? _selectedCollection;
    private bool _isRunning;
    private int _progressPercent;
    private int _totalRequests;
    private int _passedRequests;
    private int _failedRequests;
    private long _totalElapsedMs;
    private string _statusMessage = "Ready to run.";
    private CancellationTokenSource? _cts;

    public ApiCollection? SelectedCollection
    {
        get => _selectedCollection;
        set => SetProperty(ref _selectedCollection, value);
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

    public int ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    public int TotalRequests
    {
        get => _totalRequests;
        set => SetProperty(ref _totalRequests, value);
    }

    public int PassedRequests
    {
        get => _passedRequests;
        set => SetProperty(ref _passedRequests, value);
    }

    public int FailedRequests
    {
        get => _failedRequests;
        set => SetProperty(ref _failedRequests, value);
    }

    public long TotalElapsedMs
    {
        get => _totalElapsedMs;
        set => SetProperty(ref _totalElapsedMs, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<SingleRequestRunResult> Results { get; } = [];

    public ICommand RunCommand { get; }
    public ICommand StopCommand { get; }

    public CollectionRunnerViewModel(
        ICollectionRunner collectionRunner,
        Func<IDictionary<string, string>> getActiveVariables)
    {
        _collectionRunner = collectionRunner;
        _getActiveVariables = getActiveVariables;

        RunCommand = new AsyncRelayCommand(RunAsync, () => !IsRunning && SelectedCollection != null);
        StopCommand = new RelayCommand(Stop, () => IsRunning);
    }

    public async Task RunAsync()
    {
        if (SelectedCollection == null || IsRunning) return;

        IsRunning = true;
        _cts = new CancellationTokenSource();
        Results.Clear();
        TotalRequests = 0;
        PassedRequests = 0;
        FailedRequests = 0;
        TotalElapsedMs = 0;
        ProgressPercent = 0;
        StatusMessage = "Running collection tests...";

        try
        {
            var variables = _getActiveVariables();
            var runResult = await _collectionRunner.RunAsync(
                SelectedCollection,
                variables,
                onProgress: result =>
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        Results.Add(result);
                        TotalRequests++;
                        if (result.IsSuccess && result.AssertionResults.All(a => a.Passed))
                        {
                            PassedRequests++;
                        }
                        else
                        {
                            FailedRequests++;
                        }
                        TotalElapsedMs += result.ElapsedMilliseconds;
                    });
                },
                cancellationToken: _cts.Token);

            StatusMessage = $"Execution finished. {PassedRequests} passed, {FailedRequests} failed in {TotalElapsedMs} ms.";
            ProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Run was stopped by user.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Run failed: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
    }
}

public class CurlImportViewModel : ObservableObject
{
    private readonly ICurlConverter _curlConverter;
    private string _curlText = string.Empty;
    private ApiRequest? _parsedRequest;
    private string _previewSummary = "Paste a cURL command above to preview.";

    public string CurlText
    {
        get => _curlText;
        set
        {
            if (SetProperty(ref _curlText, value))
            {
                ParseCurlPreview();
            }
        }
    }

    public ApiRequest? ParsedRequest
    {
        get => _parsedRequest;
        private set => SetProperty(ref _parsedRequest, value);
    }

    public string PreviewSummary
    {
        get => _previewSummary;
        private set => SetProperty(ref _previewSummary, value);
    }

    public CurlImportViewModel(ICurlConverter curlConverter)
    {
        _curlConverter = curlConverter;
    }

    private void ParseCurlPreview()
    {
        if (string.IsNullOrWhiteSpace(CurlText))
        {
            ParsedRequest = null;
            PreviewSummary = "Paste a cURL command above to preview.";
            return;
        }

        try
        {
            var req = _curlConverter.ParseCurl(CurlText);
            ParsedRequest = req;
            PreviewSummary = $"Detected Method: {req.EffectiveMethod}\nURL: {req.Url}\nHeaders: {req.Headers.Count} headers\nBody: {req.Body.Type}";
        }
        catch (Exception ex)
        {
            ParsedRequest = null;
            PreviewSummary = $"Unable to parse cURL: {ex.Message}";
        }
    }
}
