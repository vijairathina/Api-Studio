using System.Text.RegularExpressions;
using ApiStudio.Application.Abstractions;
using ApiStudio.Domain.Entities;
using ApiStudio.Domain.ValueObjects;

namespace ApiStudio.Application.Services;

public partial class VariableResolver : IVariableResolver
{
    private static readonly Regex VariableRegex = new(@"\{\{([a-zA-Z0-9_\-\.]+)\}\}", RegexOptions.Compiled);

    public string Resolve(string input, IDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(input) || variables == null || variables.Count == 0)
        {
            return input;
        }

        return VariableRegex.Replace(input, match =>
        {
            var varName = match.Groups[1].Value;
            return variables.TryGetValue(varName, out var value) ? value : match.Value;
        });
    }

    public ApiRequest ResolveRequest(ApiRequest request, IDictionary<string, string> variables)
    {
        if (variables == null || variables.Count == 0)
        {
            return request.Clone();
        }

        var cloned = request.Clone();

        cloned.Url = Resolve(cloned.Url, variables);

        foreach (var qp in cloned.QueryParams)
        {
            qp.Key = Resolve(qp.Key, variables);
            qp.Value = Resolve(qp.Value, variables);
        }

        foreach (var header in cloned.Headers)
        {
            header.Key = Resolve(header.Key, variables);
            header.Value = Resolve(header.Value, variables);
        }

        if (cloned.Auth != null)
        {
            cloned.Auth.BearerToken = Resolve(cloned.Auth.BearerToken, variables);
            cloned.Auth.BasicUsername = Resolve(cloned.Auth.BasicUsername, variables);
            cloned.Auth.BasicPassword = Resolve(cloned.Auth.BasicPassword, variables);
            cloned.Auth.ApiKeyName = Resolve(cloned.Auth.ApiKeyName, variables);
            cloned.Auth.ApiKeyValue = Resolve(cloned.Auth.ApiKeyValue, variables);
        }

        if (cloned.Body != null)
        {
            cloned.Body.RawContent = Resolve(cloned.Body.RawContent, variables);
            foreach (var fd in cloned.Body.FormData)
            {
                fd.Key = Resolve(fd.Key, variables);
                fd.Value = Resolve(fd.Value, variables);
            }
            foreach (var ue in cloned.Body.UrlEncodedData)
            {
                ue.Key = Resolve(ue.Key, variables);
                ue.Value = Resolve(ue.Value, variables);
            }
        }

        return cloned;
    }
}
