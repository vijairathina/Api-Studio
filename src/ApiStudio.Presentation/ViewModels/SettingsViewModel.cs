using System.Windows.Input;
using ApiStudio.Application.Abstractions;
using ApiStudio.Presentation.Common;

namespace ApiStudio.Presentation.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private readonly IUserPreferencesRepository _preferencesRepository;
    private readonly IHistoryRepository _historyRepository;
    private readonly Action<bool> _applyTheme;
    private readonly Action _onHistoryCleared;

    private string _selectedTheme = "Light";
    private int _timeoutSeconds = 30;
    private bool _followRedirects = true;
    private bool _verifySsl = true;
    private bool _autoFormatJson = true;
    private int _historyCount;
    private string _statusMessage = string.Empty;

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                _applyTheme(value.Equals("Dark", StringComparison.OrdinalIgnoreCase));
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

    public bool VerifySsl
    {
        get => _verifySsl;
        set => SetProperty(ref _verifySsl, value);
    }

    public bool AutoFormatJson
    {
        get => _autoFormatJson;
        set => SetProperty(ref _autoFormatJson, value);
    }

    public int HistoryCount
    {
        get => _historyCount;
        set => SetProperty(ref _historyCount, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand ClearHistoryCommand { get; }

    public event Action? CloseRequested;

    public SettingsViewModel(
        IUserPreferencesRepository preferencesRepository,
        IHistoryRepository historyRepository,
        Action<bool> applyTheme,
        Action onHistoryCleared,
        int currentHistoryCount,
        bool isCurrentDark)
    {
        _preferencesRepository = preferencesRepository;
        _historyRepository = historyRepository;
        _applyTheme = applyTheme;
        _onHistoryCleared = onHistoryCleared;
        _historyCount = currentHistoryCount;
        _selectedTheme = isCurrentDark ? "Dark" : "Light";

        SaveCommand = new AsyncRelayCommand(SavePreferencesAsync);
        ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync);

        _ = LoadPreferencesAsync();
    }

    private async Task LoadPreferencesAsync()
    {
        var prefs = await _preferencesRepository.GetAsync();
        TimeoutSeconds = prefs.DefaultTimeoutSeconds;
        FollowRedirects = prefs.FollowRedirects;
        VerifySsl = prefs.VerifySsl;
        AutoFormatJson = prefs.AutoFormatJson;
    }

    private async Task SavePreferencesAsync()
    {
        var prefs = new UserPreferences
        {
            Theme = SelectedTheme,
            DefaultTimeoutSeconds = TimeoutSeconds,
            FollowRedirects = FollowRedirects,
            VerifySsl = VerifySsl,
            AutoFormatJson = AutoFormatJson
        };

        await _preferencesRepository.SaveAsync(prefs);
        StatusMessage = "Settings saved successfully.";
        CloseRequested?.Invoke();
    }

    private async Task ClearHistoryAsync()
    {
        await _historyRepository.ClearAsync();
        HistoryCount = 0;
        _onHistoryCleared();
        StatusMessage = "History cleared.";
    }
}
