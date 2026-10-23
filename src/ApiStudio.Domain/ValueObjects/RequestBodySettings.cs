using ApiStudio.Domain.Enums;

namespace ApiStudio.Domain.ValueObjects;

public class RequestBodySettings
{
    public BodyType Type { get; set; } = BodyType.None;
    public string RawContent { get; set; } = string.Empty;
    public List<KeyValuePairItem> FormData { get; set; } = [];
    public List<KeyValuePairItem> UrlEncodedData { get; set; } = [];
    public string BinaryFilePath { get; set; } = string.Empty;

    public RequestBodySettings Clone()
    {
        return new RequestBodySettings
        {
            Type = Type,
            RawContent = RawContent,
            FormData = FormData.Select(x => x.Clone()).ToList(),
            UrlEncodedData = UrlEncodedData.Select(x => x.Clone()).ToList(),
            BinaryFilePath = BinaryFilePath
        };
    }
}
