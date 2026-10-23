namespace ApiStudio.Domain.ValueObjects;

public class KeyValuePairItem
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    public KeyValuePairItem() { }

    public KeyValuePairItem(string key, string value, string description = "", bool isEnabled = true)
    {
        Key = key;
        Value = value;
        Description = description;
        IsEnabled = isEnabled;
    }

    public KeyValuePairItem Clone()
    {
        return new KeyValuePairItem(Key, Value, Description, IsEnabled);
    }
}
