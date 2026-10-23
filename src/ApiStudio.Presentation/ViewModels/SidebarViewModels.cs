using System.Collections.ObjectModel;
using System.Windows.Input;
using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;
using ApiStudio.Presentation.Common;

namespace ApiStudio.Presentation.ViewModels;

public enum CollectionNodeType
{
    Collection,
    Folder,
    Request
}

public class CollectionItemViewModel : ObservableObject
{
    private string _name = string.Empty;
    private bool _isExpanded = true;
    private bool _isSelected;
    private string _method = string.Empty;

    public CollectionNodeType NodeType { get; set; }
    public Guid Id { get; set; }
    public object UnderlyingModel { get; set; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Method
    {
        get => _method;
        set => SetProperty(ref _method, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ObservableCollection<CollectionItemViewModel> Children { get; } = [];

    public ICommand? OpenCommand { get; set; }
    public ICommand? AddRequestCommand { get; set; }
    public ICommand? AddFolderCommand { get; set; }
    public ICommand? DeleteCommand { get; set; }
    public ICommand? DuplicateCommand { get; set; }
    public ICommand? ExportCommand { get; set; }
    public ICommand? RunCollectionCommand { get; set; }

    public CollectionItemViewModel(CollectionNodeType nodeType, string name, object model)
    {
        NodeType = nodeType;
        _name = name;
        UnderlyingModel = model;

        if (model is ApiCollection col)
        {
            Id = col.Id;
        }
        else if (model is RequestFolder folder)
        {
            Id = folder.Id;
        }
        else if (model is ApiRequest req)
        {
            Id = req.Id;
            Method = req.EffectiveMethod;
        }
    }
}

public class HistoryItemViewModel : ObservableObject
{
    public RequestHistoryItem Model { get; }
    public DateTime ExecutedAt => Model.ExecutedAt;
    public string Method => Model.Method;
    public string Url => Model.Url;
    public int? StatusCode => Model.StatusCode;
    public string? StatusDescription => Model.StatusDescription;
    public long ElapsedMilliseconds => Model.ElapsedMilliseconds;

    public ICommand? OpenCommand { get; set; }

    public HistoryItemViewModel(RequestHistoryItem model)
    {
        Model = model;
    }
}

public class EnvironmentVariableViewModel : ObservableObject
{
    private string _key = string.Empty;
    private string _value = string.Empty;
    private bool _isSecret;
    private bool _isEnabled = true;

    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public bool IsSecret
    {
        get => _isSecret;
        set => SetProperty(ref _isSecret, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public ICommand? DeleteCommand { get; set; }

    public EnvironmentVariableViewModel() { }

    public EnvironmentVariableViewModel(EnvironmentVariable model)
    {
        _key = model.Key;
        _value = model.Value;
        _isSecret = model.IsSecret;
        _isEnabled = model.IsEnabled;
    }

    public EnvironmentVariable ToModel()
    {
        return new EnvironmentVariable(Key, Value, IsSecret, IsEnabled);
    }
}

public class EnvironmentViewModel : ObservableObject
{
    private string _name = "Environment";
    private bool _isActive;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public ObservableCollection<EnvironmentVariableViewModel> Variables { get; } = [];

    public ICommand AddVariableCommand { get; }
    public ICommand? DeleteCommand { get; set; }

    public EnvironmentViewModel()
    {
        AddVariableCommand = new RelayCommand(AddVariable);
    }

    public EnvironmentViewModel(ApiEnvironment model) : this()
    {
        Id = model.Id;
        _name = model.Name;
        _isActive = model.IsActive;

        foreach (var v in model.Variables)
        {
            AddVariableItem(v);
        }
    }

    public ApiEnvironment ToModel()
    {
        return new ApiEnvironment
        {
            Id = Id,
            Name = Name,
            IsActive = IsActive,
            Variables = Variables.Select(v => v.ToModel()).ToList()
        };
    }

    private void AddVariable()
    {
        AddVariableItem(new EnvironmentVariable());
    }

    private void AddVariableItem(EnvironmentVariable model)
    {
        var vm = new EnvironmentVariableViewModel(model);
        vm.DeleteCommand = new RelayCommand(() => Variables.Remove(vm));
        Variables.Add(vm);
    }
}
