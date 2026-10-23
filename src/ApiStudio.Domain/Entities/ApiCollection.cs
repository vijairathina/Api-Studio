namespace ApiStudio.Domain.Entities;

public class RequestFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Folder";
    public string Description { get; set; } = string.Empty;
    public List<ApiRequest> Requests { get; set; } = [];
    public List<RequestFolder> SubFolders { get; set; } = [];

    public RequestFolder Clone()
    {
        return new RequestFolder
        {
            Id = Guid.NewGuid(),
            Name = $"{Name} (Copy)",
            Description = Description,
            Requests = Requests.Select(r => r.Clone()).ToList(),
            SubFolders = SubFolders.Select(f => f.Clone()).ToList()
        };
    }
}

public class ApiCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Collection";
    public string Description { get; set; } = string.Empty;
    public List<ApiRequest> Requests { get; set; } = [];
    public List<RequestFolder> Folders { get; set; } = [];
    public List<EnvironmentVariable> Variables { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    public ApiCollection Clone()
    {
        return new ApiCollection
        {
            Id = Guid.NewGuid(),
            Name = $"{Name} (Copy)",
            Description = Description,
            Requests = Requests.Select(r => r.Clone()).ToList(),
            Folders = Folders.Select(f => f.Clone()).ToList(),
            Variables = Variables.Select(v => v.Clone()).ToList(),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }
}
