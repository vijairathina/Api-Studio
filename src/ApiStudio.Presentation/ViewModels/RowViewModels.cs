using System.Windows.Input;
using ApiStudio.Domain.Enums;
using ApiStudio.Domain.ValueObjects;
using ApiStudio.Presentation.Common;

namespace ApiStudio.Presentation.ViewModels;

public class KeyValuePairViewModel : ObservableObject
{
    private string _key = string.Empty;
    private string _value = string.Empty;
    private string _description = string.Empty;
    private bool _isEnabled = true;

    public event Action? Changed;

    public string Key
    {
        get => _key;
        set
        {
            if (SetProperty(ref _key, value))
            {
                Changed?.Invoke();
            }
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                Changed?.Invoke();
            }
        }
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                Changed?.Invoke();
            }
        }
    }

    public ICommand? DeleteCommand { get; set; }

    public KeyValuePairViewModel() { }

    public KeyValuePairViewModel(string key, string value, string description = "", bool isEnabled = true)
    {
        _key = key;
        _value = value;
        _description = description;
        _isEnabled = isEnabled;
    }

    public KeyValuePairItem ToModel()
    {
        return new KeyValuePairItem(Key, Value, Description, IsEnabled);
    }

    public static KeyValuePairViewModel FromModel(KeyValuePairItem model)
    {
        return new KeyValuePairViewModel(model.Key, model.Value, model.Description, model.IsEnabled);
    }
}

public class AssertionItemViewModel : ObservableObject
{
    private AssertionTarget _target = AssertionTarget.StatusCode;
    private string _propertyName = string.Empty;
    private AssertionOperator _operator = AssertionOperator.Equals;
    private string _expectedValue = "200";
    private bool _isEnabled = true;

    public Guid Id { get; set; } = Guid.NewGuid();

    public AssertionTarget Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }

    public string PropertyName
    {
        get => _propertyName;
        set => SetProperty(ref _propertyName, value);
    }

    public AssertionOperator Operator
    {
        get => _operator;
        set => SetProperty(ref _operator, value);
    }

    public string ExpectedValue
    {
        get => _expectedValue;
        set => SetProperty(ref _expectedValue, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public ICommand? DeleteCommand { get; set; }
}
