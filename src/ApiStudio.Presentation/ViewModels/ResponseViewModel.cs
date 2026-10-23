using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using ApiStudio.Domain.Entities;
using ApiStudio.Presentation.Common;
using Microsoft.Win32;

namespace ApiStudio.Presentation.ViewModels;

public class ResponseViewModel : ObservableObject
{
    private int _statusCode;
    private string _statusDescription = string.Empty;
    private long _elapsedMilliseconds;
    private long _contentSizeBytes;
    private string _body = string.Empty;
    private string _formattedBody = string.Empty;
    private string? _errorMessage;
    private string _searchText = string.Empty;
    private bool _hasResponse;
    private int _selectedResponseTab = 0;

    public int StatusCode
    {
        get => _statusCode;
        set => SetProperty(ref _statusCode, value);
    }

    public string StatusDescription
    {
        get => _statusDescription;
        set => SetProperty(ref _statusDescription, value);
    }

    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;

    public long ElapsedMilliseconds
    {
        get => _elapsedMilliseconds;
        set => SetProperty(ref _elapsedMilliseconds, value);
    }

    public long ContentSizeBytes
    {
        get => _contentSizeBytes;
        set => SetProperty(ref _contentSizeBytes, value);
    }

    public string Body
    {
        get => _body;
        set
        {
            if (SetProperty(ref _body, value))
            {
                FormattedBody = FormatBodyContent(value);
            }
        }
    }

    public string FormattedBody
    {
        get => _formattedBody;
        set => SetProperty(ref _formattedBody, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool HasResponse
    {
        get => _hasResponse;
        set => SetProperty(ref _hasResponse, value);
    }

    public int SelectedResponseTab
    {
        get => _selectedResponseTab;
        set => SetProperty(ref _selectedResponseTab, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public ObservableCollection<KeyValuePair<string, string>> Headers { get; } = [];
    public ObservableCollection<string> Cookies { get; } = [];
    public ObservableCollection<AssertionResult> TestResults { get; } = [];

    public ICommand CopyBodyCommand { get; }
    public ICommand SaveToFileCommand { get; }

    public ResponseViewModel()
    {
        CopyBodyCommand = new RelayCommand(CopyBody);
        SaveToFileCommand = new RelayCommand(SaveToFile);
    }

    public void LoadFromResponse(ApiResponse response)
    {
        StatusCode = response.StatusCode;
        StatusDescription = response.StatusDescription;
        ElapsedMilliseconds = response.ElapsedMilliseconds;
        ContentSizeBytes = response.ContentSizeBytes;
        ErrorMessage = response.ErrorMessage;
        Body = response.Body ?? string.Empty;
        HasResponse = true;

        Headers.Clear();
        foreach (var h in response.Headers)
        {
            Headers.Add(h);
        }

        Cookies.Clear();
        foreach (var c in response.Cookies)
        {
            Cookies.Add(c);
        }

        TestResults.Clear();
        foreach (var t in response.TestResults)
        {
            TestResults.Add(t);
        }

        // If there is an error message, show the body or error
        if (!string.IsNullOrEmpty(response.ErrorMessage) && string.IsNullOrEmpty(Body))
        {
            FormattedBody = response.ErrorMessage;
        }
    }

    public void Reset()
    {
        StatusCode = 0;
        StatusDescription = string.Empty;
        ElapsedMilliseconds = 0;
        ContentSizeBytes = 0;
        ErrorMessage = null;
        Body = string.Empty;
        FormattedBody = string.Empty;
        HasResponse = false;
        Headers.Clear();
        Cookies.Clear();
        TestResults.Clear();
    }

    private void CopyBody()
    {
        if (!string.IsNullOrEmpty(Body))
        {
            Clipboard.SetText(Body);
        }
    }

    private void SaveToFile()
    {
        if (string.IsNullOrEmpty(Body)) return;

        var saveDialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = "response.json"
        };

        if (saveDialog.ShowDialog() == true)
        {
            File.WriteAllText(saveDialog.FileName, Body);
        }
    }

    private static string FormatBodyContent(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var trimmed = raw.Trim();

        // Try JSON format
        if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) || 
            (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return raw;
            }
        }

        // Try XML format
        if (trimmed.StartsWith("<") && trimmed.EndsWith(">"))
        {
            try
            {
                var doc = XDocument.Parse(trimmed);
                return doc.ToString();
            }
            catch
            {
                return raw;
            }
        }

        return raw;
    }
}
