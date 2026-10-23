using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using ApiStudio.Application.Abstractions;
using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;
using ApiStudio.Domain.ValueObjects;
using ApiStudio.Presentation.Common;
using Microsoft.Win32;

namespace ApiStudio.Presentation.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly IHttpRequestExecutor _requestExecutor;
    private readonly IVariableResolver _variableResolver;
    private readonly IAssertionEvaluator _assertionEvaluator;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IHistoryRepository _historyRepository;
    private readonly IUserPreferencesRepository _preferencesRepository;
    private readonly ICurlConverter _curlConverter;
    private readonly IImportExportService _importExportService;
    private readonly ICollectionRunner _collectionRunner;

    private RequestTabViewModel? _selectedTab;
    private EnvironmentViewModel? _selectedEnvironment;
    private int _selectedSidebarTab = 0;
    private string _searchFilter = string.Empty;
    private bool _isSidebarCollapsed;
    private bool _isDarkTheme;

    private List<ApiCollection> _rawCollections = [];

    public ObservableCollection<RequestTabViewModel> Tabs { get; } = [];
    public ObservableCollection<CollectionItemViewModel> CollectionNodes { get; } = [];
    public ObservableCollection<HistoryItemViewModel> HistoryItems { get; } = [];
    public ObservableCollection<EnvironmentViewModel> Environments { get; } = [];

    public RequestTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab != null)
            {
                _selectedTab.IsSelected = false;
            }
            if (SetProperty(ref _selectedTab, value))
            {
                if (_selectedTab != null)
                {
                    _selectedTab.IsSelected = true;
                }
            }
        }
    }

    public EnvironmentViewModel? SelectedEnvironment
    {
        get => _selectedEnvironment;
        set
        {
            if (SetProperty(ref _selectedEnvironment, value))
            {
                if (value != null)
                {
                    foreach (var env in Environments)
                    {
                        env.IsActive = (env.Id == value.Id);
                    }
                    _ = _environmentRepository.SetActiveAsync(value.Id);
                }
            }
        }
    }

    public int SelectedSidebarTab
    {
        get => _selectedSidebarTab;
        set
        {
            if (SetProperty(ref _selectedSidebarTab, value))
            {
                if (value == 1)
                {
                    _ = RefreshHistoryAsync();
                }
            }
        }
    }

    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
            {
                ApplyCollectionFilter();
            }
        }
    }

    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set => SetProperty(ref _isSidebarCollapsed, value);
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set => SetProperty(ref _isDarkTheme, value);
    }

    // Commands
    public ICommand NewTabCommand { get; }
    public ICommand SelectTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand SaveActiveTabCommand { get; }
    public ICommand AddCollectionCommand { get; }
    public ICommand ImportCollectionCommand { get; }
    public ICommand ExportCollectionCommand { get; }
    public ICommand ImportCurlCommand { get; }
    public ICommand OpenRunnerCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public MainViewModel(
        IHttpRequestExecutor requestExecutor,
        IVariableResolver variableResolver,
        IAssertionEvaluator assertionEvaluator,
        ICollectionRepository collectionRepository,
        IEnvironmentRepository environmentRepository,
        IHistoryRepository historyRepository,
        IUserPreferencesRepository preferencesRepository,
        ICurlConverter curlConverter,
        IImportExportService importExportService,
        ICollectionRunner collectionRunner)
    {
        _requestExecutor = requestExecutor;
        _variableResolver = variableResolver;
        _assertionEvaluator = assertionEvaluator;
        _collectionRepository = collectionRepository;
        _environmentRepository = environmentRepository;
        _historyRepository = historyRepository;
        _preferencesRepository = preferencesRepository;
        _curlConverter = curlConverter;
        _importExportService = importExportService;
        _collectionRunner = collectionRunner;

        NewTabCommand = new RelayCommand(AddNewTab);
        SelectTabCommand = new RelayCommand<RequestTabViewModel>(SelectTab);
        CloseTabCommand = new RelayCommand<RequestTabViewModel>(CloseTab);
        SaveActiveTabCommand = new RelayCommand(SaveActiveTab);
        AddCollectionCommand = new RelayCommand(AddCollection);
        ImportCollectionCommand = new RelayCommand(ImportCollection);
        ExportCollectionCommand = new RelayCommand<CollectionItemViewModel>(ExportCollection);
        ImportCurlCommand = new RelayCommand(OpenCurlImport);
        OpenRunnerCommand = new RelayCommand<CollectionItemViewModel>(OpenRunner);
        ClearHistoryCommand = new RelayCommand(ClearHistory);
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarCollapsed = !IsSidebarCollapsed);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
    }

    public async Task InitializeAsync()
    {
        // 1. Load Environments
        var envs = await _environmentRepository.GetAllAsync();
        Environments.Clear();
        foreach (var env in envs)
        {
            var envVm = new EnvironmentViewModel(env);
            envVm.DeleteCommand = new RelayCommand(() => DeleteEnvironment(envVm));
            Environments.Add(envVm);
        }
        SelectedEnvironment = Environments.FirstOrDefault(e => e.IsActive) ?? Environments.FirstOrDefault();

        // 2. Load Collections
        _rawCollections = await _collectionRepository.GetAllAsync();
        BuildCollectionTree();

        // 3. Load History
        await RefreshHistoryAsync();

        // 4. Open default tab or first request
        if (_rawCollections.Count > 0 && _rawCollections[0].Requests.Count > 0)
        {
            OpenRequestInTab(_rawCollections[0].Requests[0]);
        }
        else
        {
            AddNewTab();
        }
    }

    public IDictionary<string, string> GetActiveVariables()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (SelectedEnvironment != null)
        {
            foreach (var v in SelectedEnvironment.Variables.Where(v => v.IsEnabled && !string.IsNullOrWhiteSpace(v.Key)))
            {
                dict[v.Key] = v.Value;
            }
        }

        return dict;
    }

    public RequestTabViewModel CreateNewTabViewModel()
    {
        var tab = new RequestTabViewModel(
            _requestExecutor,
            _variableResolver,
            _assertionEvaluator,
            _historyRepository,
            GetActiveVariables);

        tab.SaveRequested += OnTabSaveRequested;
        tab.HistoryItemAdded += OnHistoryItemAdded;
        return tab;
    }

    private void OnHistoryItemAdded(RequestHistoryItem item)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            var vm = new HistoryItemViewModel(item)
            {
                OpenCommand = new RelayCommand(() => OpenRequestInTab(item.RequestSnapshot))
            };
            HistoryItems.Insert(0, vm);
        });
    }

    public void SelectTab(RequestTabViewModel? tab)
    {
        if (tab != null && Tabs.Contains(tab))
        {
            SelectedTab = tab;
        }
    }

    public void AddNewTab()
    {
        var tab = CreateNewTabViewModel();
        tab.Name = $"Request {Tabs.Count + 1}";
        tab.Url = "https://jsonplaceholder.typicode.com/posts/1";
        tab.Method = HttpMethodType.GET;
        tab.IsDirty = false;
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    public void OpenRequestInTab(ApiRequest request)
    {
        // Check if already open
        var existing = Tabs.FirstOrDefault(t => 
            (request.Id != Guid.Empty && t.Id == request.Id) ||
            (t.Url == request.Url && t.Method == request.Method && t.Name == request.Name));

        if (existing != null)
        {
            SelectedTab = existing;
            return;
        }

        // If active tab is untouched/default, reuse it
        if (SelectedTab != null && !SelectedTab.IsDirty && !SelectedTab.Response.HasResponse &&
            SelectedTab.Name.StartsWith("Request ") &&
            (SelectedTab.Url == "https://jsonplaceholder.typicode.com/posts/1" || string.IsNullOrWhiteSpace(SelectedTab.Url)))
        {
            SelectedTab.LoadFromModel(request);
            return;
        }

        var tab = CreateNewTabViewModel();
        tab.LoadFromModel(request);
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    public void CloseTab(RequestTabViewModel? tab)
    {
        if (tab == null) return;
        var idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (SelectedTab == tab)
        {
            SelectedTab = Tabs.Count > 0 ? Tabs[Math.Max(0, idx - 1)] : null;
        }

        if (Tabs.Count == 0)
        {
            AddNewTab();
        }
    }

    private void SaveActiveTab()
    {
        if (SelectedTab != null)
        {
            OnTabSaveRequested(SelectedTab);
        }
    }

    private async void OnTabSaveRequested(RequestTabViewModel tab)
    {
        var updated = tab.ToModel();

        // Find if request belongs to any collection
        bool found = false;
        foreach (var col in _rawCollections)
        {
            var reqIdx = col.Requests.FindIndex(r => r.Id == updated.Id);
            if (reqIdx >= 0)
            {
                col.Requests[reqIdx] = updated;
                found = true;
                break;
            }

            foreach (var folder in col.Folders)
            {
                var fIdx = folder.Requests.FindIndex(r => r.Id == updated.Id);
                if (fIdx >= 0)
                {
                    folder.Requests[fIdx] = updated;
                    found = true;
                    break;
                }
            }
            if (found) break;
        }

        if (!found)
        {
            if (_rawCollections.Count == 0)
            {
                _rawCollections.Add(new ApiCollection { Name = "My Collection" });
            }
            _rawCollections[0].Requests.Add(updated);
        }

        await _collectionRepository.SaveAllAsync(_rawCollections);
        tab.IsDirty = false;
        BuildCollectionTree();
    }

    public void AddCollection()
    {
        var col = new ApiCollection
        {
            Name = $"New Collection {_rawCollections.Count + 1}"
        };
        _rawCollections.Add(col);
        _ = _collectionRepository.SaveAllAsync(_rawCollections);
        BuildCollectionTree();
    }

    public async Task RefreshHistoryAsync()
    {
        var history = await _historyRepository.GetAllAsync();
        HistoryItems.Clear();
        foreach (var h in history)
        {
            var vm = new HistoryItemViewModel(h)
            {
                OpenCommand = new RelayCommand(() => OpenRequestInTab(h.RequestSnapshot))
            };
            HistoryItems.Add(vm);
        }
    }

    public async void ClearHistory()
    {
        await _historyRepository.ClearAsync();
        HistoryItems.Clear();
    }

    private void ImportCollection()
    {
        var openDialog = new OpenFileDialog
        {
            Title = "Import Collection",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        };

        if (openDialog.ShowDialog() == true)
        {
            try
            {
                var content = File.ReadAllText(openDialog.FileName);
                ApiCollection col;
                if (_importExportService.IsPostmanCollection(content))
                {
                    col = _importExportService.ImportPostmanCollection(content);
                }
                else
                {
                    col = _importExportService.ImportCollection(content);
                }

                _rawCollections.Add(col);
                _ = _collectionRepository.SaveAllAsync(_rawCollections);
                BuildCollectionTree();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to import collection: {ex.Message}", "Import Error");
            }
        }
    }

    private void ExportCollection(CollectionItemViewModel? node)
    {
        ApiCollection? col = null;
        if (node?.UnderlyingModel is ApiCollection c)
        {
            col = c;
        }
        else if (_rawCollections.Count > 0)
        {
            col = _rawCollections[0];
        }

        if (col == null) return;

        var saveDialog = new SaveFileDialog
        {
            Title = "Export Collection",
            Filter = "JSON Files (*.json)|*.json",
            FileName = $"{col.Name.Replace(" ", "_")}.json"
        };

        if (saveDialog.ShowDialog() == true)
        {
            var json = _importExportService.ExportCollection(col);
            File.WriteAllText(saveDialog.FileName, json);
        }
    }

    private void OpenCurlImport()
    {
        var vm = new CurlImportViewModel(_curlConverter);
        var win = new Views.Controls.CurlImportDialog
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (win.ShowDialog() == true && vm.ParsedRequest != null)
        {
            OpenRequestInTab(vm.ParsedRequest);
        }
    }

    private void OpenRunner(CollectionItemViewModel? node)
    {
        ApiCollection? targetCollection = null;
        if (node?.UnderlyingModel is ApiCollection c)
        {
            targetCollection = c;
        }
        else if (_rawCollections.Count > 0)
        {
            targetCollection = _rawCollections[0];
        }

        if (targetCollection == null) return;

        var vm = new CollectionRunnerViewModel(_collectionRunner, GetActiveVariables)
        {
            SelectedCollection = targetCollection
        };

        var win = new Views.Controls.CollectionRunnerWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current.MainWindow
        };

        win.ShowDialog();
    }

    private void OpenSettings()
    {
        var vm = new SettingsViewModel(
            _preferencesRepository,
            _historyRepository,
            applyTheme: isDark =>
            {
                IsDarkTheme = isDark;
                ApplyTheme(isDark);
            },
            onHistoryCleared: () => HistoryItems.Clear(),
            currentHistoryCount: HistoryItems.Count,
            isCurrentDark: IsDarkTheme);

        var win = new Views.Controls.SettingsWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current.MainWindow
        };

        win.ShowDialog();
    }

    private async void DeleteEnvironment(EnvironmentViewModel env)
    {
        Environments.Remove(env);
        var list = Environments.Select(e => e.ToModel()).ToList();
        await _environmentRepository.SaveAllAsync(list);
        if (SelectedEnvironment == env)
        {
            SelectedEnvironment = Environments.FirstOrDefault();
        }
    }

    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplyTheme(IsDarkTheme);
    }

    private static void ApplyTheme(bool isDark)
    {
        var appResources = System.Windows.Application.Current.Resources;

        if (isDark)
        {
            appResources["LightBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0B132B"));
            appResources["PanelBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#131D3B"));
            appResources["GlassCardBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E6131D3B"));
            appResources["GlassSubtleBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B31C284D"));
            appResources["SubtleBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E2C52"));
            appResources["HoverBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#253664"));
            appResources["PrimaryTextBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F8FAFC"));
            appResources["SecondaryTextBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8"));
            appResources["BorderBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#263859"));

            var darkPanelGrad = new System.Windows.Media.LinearGradientBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F0131E3D"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D90E162E"),
                new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
            appResources["GlassPanelGradient"] = darkPanelGrad;

            var darkBorderGrad = new System.Windows.Media.LinearGradientBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4D38507A"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#261E2E4A"),
                new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
            appResources["GlassBorderGradient"] = darkBorderGrad;
        }
        else
        {
            appResources["LightBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EDF3FA"));
            appResources["PanelBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
            appResources["GlassCardBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F2FFFFFF"));
            appResources["GlassSubtleBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A0EDF4FB"));
            appResources["SubtleBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8F0FA"));
            appResources["HoverBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2ECF8"));
            appResources["PrimaryTextBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F1D38"));
            appResources["SecondaryTextBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5A6A85"));
            appResources["BorderBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D2DEF2"));

            var lightPanelGrad = new System.Windows.Media.LinearGradientBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5FFFFFF"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D6F0F6FC"),
                new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
            appResources["GlassPanelGradient"] = lightPanelGrad;

            var lightBorderGrad = new System.Windows.Media.LinearGradientBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B3FFFFFF"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#40D0E2F5"),
                new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
            appResources["GlassBorderGradient"] = lightBorderGrad;
        }
    }

    private void BuildCollectionTree()
    {
        CollectionNodes.Clear();

        foreach (var col in _rawCollections)
        {
            var colNode = new CollectionItemViewModel(CollectionNodeType.Collection, col.Name, col)
            {
                AddRequestCommand = new RelayCommand(() => AddRequestToCollection(col)),
                AddFolderCommand = new RelayCommand(() => AddFolderToCollection(col)),
                DeleteCommand = new RelayCommand(() => DeleteCollection(col)),
                ExportCommand = new RelayCommand(() => ExportCollection(null)),
                RunCollectionCommand = new RelayCommand(() => OpenRunner(null))
            };

            foreach (var folder in col.Folders)
            {
                var folderNode = BuildFolderNode(folder, col);
                colNode.Children.Add(folderNode);
            }

            foreach (var req in col.Requests)
            {
                var reqNode = new CollectionItemViewModel(CollectionNodeType.Request, req.Name, req)
                {
                    OpenCommand = new RelayCommand(() => OpenRequestInTab(req)),
                    DeleteCommand = new RelayCommand(() => DeleteRequestFromCollection(col, req)),
                    DuplicateCommand = new RelayCommand(() => DuplicateRequest(col, req))
                };
                colNode.Children.Add(reqNode);
            }

            CollectionNodes.Add(colNode);
        }
    }

    private CollectionItemViewModel BuildFolderNode(RequestFolder folder, ApiCollection col)
    {
        var folderNode = new CollectionItemViewModel(CollectionNodeType.Folder, folder.Name, folder)
        {
            AddRequestCommand = new RelayCommand(() => AddRequestToFolder(folder)),
            DeleteCommand = new RelayCommand(() => DeleteFolder(col, folder))
        };

        foreach (var sub in folder.SubFolders)
        {
            folderNode.Children.Add(BuildFolderNode(sub, col));
        }

        foreach (var req in folder.Requests)
        {
            var reqNode = new CollectionItemViewModel(CollectionNodeType.Request, req.Name, req)
            {
                OpenCommand = new RelayCommand(() => OpenRequestInTab(req)),
                DeleteCommand = new RelayCommand(() => DeleteRequestFromFolder(folder, req)),
                DuplicateCommand = new RelayCommand(() => DuplicateRequestInFolder(folder, req))
            };
            folderNode.Children.Add(reqNode);
        }

        return folderNode;
    }

    private void AddRequestToCollection(ApiCollection col)
    {
        var newReq = new ApiRequest
        {
            Name = $"New Request {col.Requests.Count + 1}",
            Url = "https://"
        };
        col.Requests.Add(newReq);
        _ = _collectionRepository.SaveAllAsync(_rawCollections);
        BuildCollectionTree();
        OpenRequestInTab(newReq);
    }

    private void AddFolderToCollection(ApiCollection col)
    {
        var newFolder = new RequestFolder
        {
            Name = $"New Folder {col.Folders.Count + 1}"
        };
        col.Folders.Add(newFolder);
        _ = _collectionRepository.SaveAllAsync(_rawCollections);
        BuildCollectionTree();
    }

    private void AddRequestToFolder(RequestFolder folder)
    {
        var newReq = new ApiRequest
        {
            Name = $"New Request {folder.Requests.Count + 1}",
            Url = "https://"
        };
        folder.Requests.Add(newReq);
        _ = _collectionRepository.SaveAllAsync(_rawCollections);
        BuildCollectionTree();
        OpenRequestInTab(newReq);
    }

    private void DeleteCollection(ApiCollection col)
    {
        _rawCollections.Remove(col);
        _ = _collectionRepository.SaveAllAsync(_rawCollections);
        BuildCollectionTree();
    }

    private void DeleteFolder(ApiCollection col, RequestFolder folder)
    {
        col.Folders.Remove(folder);
        _ = _collectionRepository.SaveAllAsync(_rawCollections);
        BuildCollectionTree();
    }

    private void DeleteRequestFromCollection(ApiCollection col, ApiRequest req)
    {
        col.Requests.Remove(req);
        _ = _collectionRepository.SaveAllAsync(_rawCollections);
        BuildCollectionTree();
    }

    private void DeleteRequestFromFolder(RequestFolder folder, ApiRequest req)
    {
        folder.Requests.Remove(req);
        _ = _collectionRepository.SaveAllAsync(_rawCollections);
        BuildCollectionTree();
    }

    private void DuplicateRequest(ApiCollection col, ApiRequest req)
    {
        var clone = req.Clone();
        clone.Name = $"{req.Name} (Copy)";
        col.Requests.Add(clone);
        _ = _collectionRepository.SaveAllAsync(_rawCollections);
        BuildCollectionTree();
        OpenRequestInTab(clone);
    }

    private void DuplicateRequestInFolder(RequestFolder folder, ApiRequest req)
    {
        var clone = req.Clone();
        clone.Name = $"{req.Name} (Copy)";
        folder.Requests.Add(clone);
        _ = _collectionRepository.SaveAllAsync(_rawCollections);
        BuildCollectionTree();
        OpenRequestInTab(clone);
    }

    private void ApplyCollectionFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchFilter))
        {
            BuildCollectionTree();
            return;
        }

        var filter = SearchFilter.Trim().ToLowerInvariant();
        CollectionNodes.Clear();

        foreach (var col in _rawCollections)
        {
            var matchesCol = col.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
            var matchingRequests = col.Requests.Where(r => r.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || r.Url.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchesCol || matchingRequests.Count > 0)
            {
                var colNode = new CollectionItemViewModel(CollectionNodeType.Collection, col.Name, col);
                foreach (var req in matchingRequests)
                {
                    colNode.Children.Add(new CollectionItemViewModel(CollectionNodeType.Request, req.Name, req)
                    {
                        OpenCommand = new RelayCommand(() => OpenRequestInTab(req))
                    });
                }
                CollectionNodes.Add(colNode);
            }
        }
    }
}
