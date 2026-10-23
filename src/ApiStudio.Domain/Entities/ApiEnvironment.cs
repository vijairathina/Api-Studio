namespace ApiStudio.Domain.Entities;

public class EnvironmentVariable
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsSecret { get; set; }
    public bool IsEnabled { get; set; } = true;

    public EnvironmentVariable() { }

    public EnvironmentVariable(string key, string value, bool isSecret = false, bool isEnabled = true)
    {
        Key = key;
        Value = value;
        IsSecret = isSecret;
        IsEnabled = isEnabled;
    }

    public EnvironmentVariable Clone()
    {
        return new EnvironmentVariable(Key, Value, IsSecret, IsEnabled);
    }
}

public class ApiEnvironment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Environment";
    public bool IsActive { get; set; }
    public List<EnvironmentVariable> Variables { get; set; } = [];

    public ApiEnvironment Clone()
    {
        return new ApiEnvironment
        {
            Id = Guid.NewGuid(),
            Name = $"{Name} (Copy)",
            IsActive = false,
            Variables = Variables.Select(v => v.Clone()).ToList()
        };
    }
}
