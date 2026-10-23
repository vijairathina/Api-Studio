using System.Text;
using System.Text.RegularExpressions;
using ApiStudio.Application.Abstractions;
using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;
using ApiStudio.Domain.ValueObjects;

namespace ApiStudio.Application.Services;

public class CurlConverter : ICurlConverter
{
    public string ExportCurl(ApiRequest request)
    {
        var sb = new StringBuilder();
        sb.Append("curl --location");

        // Method
        var method = request.EffectiveMethod;
        if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append($" --request {method}");
        }

        // URL
        sb.Append($" \"{EscapeQuotes(request.Url)}\"");

        // Headers
        foreach (var h in request.Headers.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key)))
        {
            sb.Append($" \\\n  --header \"{EscapeQuotes(h.Key)}: {EscapeQuotes(h.Value)}\"");
        }

        // Auth
        if (request.Auth != null)
        {
            switch (request.Auth.Type)
            {
                case AuthType.Bearer when !string.IsNullOrWhiteSpace(request.Auth.BearerToken):
                    sb.Append($" \\\n  --header \"Authorization: Bearer {EscapeQuotes(request.Auth.BearerToken)}\"");
                    break;
                case AuthType.Basic:
                    sb.Append($" \\\n  --user \"{EscapeQuotes(request.Auth.BasicUsername)}:{EscapeQuotes(request.Auth.BasicPassword)}\"");
                    break;
                case AuthType.ApiKey when request.Auth.ApiKeyLocation == ApiKeyLocation.Header && !string.IsNullOrWhiteSpace(request.Auth.ApiKeyName):
                    sb.Append($" \\\n  --header \"{EscapeQuotes(request.Auth.ApiKeyName)}: {EscapeQuotes(request.Auth.ApiKeyValue)}\"");
                    break;
            }
        }

        // Body
        if (request.Body != null)
        {
            switch (request.Body.Type)
            {
                case BodyType.Json:
                case BodyType.Xml:
                case BodyType.Text:
                    if (!string.IsNullOrEmpty(request.Body.RawContent))
                    {
                        var contentType = request.Body.Type switch
                        {
                            BodyType.Json => "application/json",
                            BodyType.Xml => "application/xml",
                            _ => "text/plain"
                        };
                        if (!request.Headers.Any(h => h.IsEnabled && string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)))
                        {
                            sb.Append($" \\\n  --header \"Content-Type: {contentType}\"");
                        }
                        sb.Append($" \\\n  --data-raw '{request.Body.RawContent.Replace("'", "'\\''")}'");
                    }
                    break;

                case BodyType.FormUrlEncoded:
                    foreach (var item in request.Body.UrlEncodedData.Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.Key)))
                    {
                        sb.Append($" \\\n  --data-urlencode \"{EscapeQuotes(item.Key)}={EscapeQuotes(item.Value)}\"");
                    }
                    break;

                case BodyType.MultipartFormData:
                    foreach (var item in request.Body.FormData.Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.Key)))
                    {
                        sb.Append($" \\\n  --form \"{EscapeQuotes(item.Key)}={EscapeQuotes(item.Value)}\"");
                    }
                    break;
            }
        }

        return sb.ToString();
    }

    public ApiRequest ParseCurl(string curlCommand)
    {
        var request = new ApiRequest
        {
            Name = "Imported cURL Request"
        };

        if (string.IsNullOrWhiteSpace(curlCommand))
        {
            return request;
        }

        var tokens = TokenizeCommandLine(curlCommand);
        var methodExplicit = false;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token.Equals("curl", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if ((token == "-X" || token == "--request") && i + 1 < tokens.Count)
            {
                var methodStr = tokens[++i].ToUpperInvariant();
                if (Enum.TryParse<HttpMethodType>(methodStr, true, out var parsedMethod))
                {
                    request.Method = parsedMethod;
                }
                else
                {
                    request.Method = HttpMethodType.CUSTOM;
                    request.CustomMethod = methodStr;
                }
                methodExplicit = true;
            }
            else if ((token == "-H" || token == "--header") && i + 1 < tokens.Count)
            {
                var headerVal = tokens[++i];
                var separatorIdx = headerVal.IndexOf(':');
                if (separatorIdx > 0)
                {
                    var key = headerVal[..separatorIdx].Trim();
                    var val = headerVal[(separatorIdx + 1)..].Trim();

                    // Check if Authorization header
                    if (string.Equals(key, "Authorization", StringComparison.OrdinalIgnoreCase) && 
                        val.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        request.Auth.Type = AuthType.Bearer;
                        request.Auth.BearerToken = val[7..].Trim();
                    }
                    else
                    {
                        request.Headers.Add(new KeyValuePairItem(key, val));
                    }
                }
            }
            else if ((token == "-u" || token == "--user") && i + 1 < tokens.Count)
            {
                var userPass = tokens[++i];
                var separatorIdx = userPass.IndexOf(':');
                request.Auth.Type = AuthType.Basic;
                if (separatorIdx >= 0)
                {
                    request.Auth.BasicUsername = userPass[..separatorIdx];
                    request.Auth.BasicPassword = userPass[(separatorIdx + 1)..];
                }
                else
                {
                    request.Auth.BasicUsername = userPass;
                }
            }
            else if ((token == "-d" || token == "--data" || token == "--data-raw" || token == "--data-binary") && i + 1 < tokens.Count)
            {
                var bodyContent = tokens[++i];
                request.Body.RawContent = bodyContent;
                if (!methodExplicit && request.Method == HttpMethodType.GET)
                {
                    request.Method = HttpMethodType.POST;
                }

                // Detect JSON or Text
                var trimmed = bodyContent.Trim();
                if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) || 
                    (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
                {
                    request.Body.Type = BodyType.Json;
                }
                else if (trimmed.StartsWith("<") && trimmed.EndsWith(">"))
                {
                    request.Body.Type = BodyType.Xml;
                }
                else
                {
                    request.Body.Type = BodyType.Text;
                }
            }
            else if (token == "--data-urlencode" && i + 1 < tokens.Count)
            {
                var data = tokens[++i];
                request.Body.Type = BodyType.FormUrlEncoded;
                if (!methodExplicit && request.Method == HttpMethodType.GET)
                {
                    request.Method = HttpMethodType.POST;
                }

                var eqIdx = data.IndexOf('=');
                if (eqIdx > 0)
                {
                    request.Body.UrlEncodedData.Add(new KeyValuePairItem(data[..eqIdx], data[(eqIdx + 1)..]));
                }
                else
                {
                    request.Body.UrlEncodedData.Add(new KeyValuePairItem(data, ""));
                }
            }
            else if ((token == "-F" || token == "--form") && i + 1 < tokens.Count)
            {
                var formVal = tokens[++i];
                request.Body.Type = BodyType.MultipartFormData;
                if (!methodExplicit && request.Method == HttpMethodType.GET)
                {
                    request.Method = HttpMethodType.POST;
                }

                var eqIdx = formVal.IndexOf('=');
                if (eqIdx > 0)
                {
                    request.Body.FormData.Add(new KeyValuePairItem(formVal[..eqIdx], formVal[(eqIdx + 1)..]));
                }
                else
                {
                    request.Body.FormData.Add(new KeyValuePairItem(formVal, ""));
                }
            }
            else if (!token.StartsWith("-") && (token.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                                               token.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                                               token.Contains("://") ||
                                               token.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)))
            {
                request.Url = token;
            }
        }

        // Parse query params from URL if present
        ParseQueryParamsFromUrl(request);

        return request;
    }

    private static void ParseQueryParamsFromUrl(ApiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url)) return;

        var qIndex = request.Url.IndexOf('?');
        if (qIndex >= 0 && qIndex < request.Url.Length - 1)
        {
            var queryString = request.Url[(qIndex + 1)..];
            var pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var eqIdx = pair.IndexOf('=');
                if (eqIdx >= 0)
                {
                    var key = Uri.UnescapeDataString(pair[..eqIdx]);
                    var val = Uri.UnescapeDataString(pair[(eqIdx + 1)..]);
                    request.QueryParams.Add(new KeyValuePairItem(key, val));
                }
                else
                {
                    request.QueryParams.Add(new KeyValuePairItem(Uri.UnescapeDataString(pair), ""));
                }
            }
        }
    }

    private static string EscapeQuotes(string input)
    {
        return input.Replace("\"", "\\\"");
    }

    private static List<string> TokenizeCommandLine(string commandLine)
    {
        var tokens = new List<string>();
        var currentToken = new StringBuilder();
        bool inQuotes = false;
        char quoteChar = '\0';
        bool isEscaped = false;

        // Clean newline continuations (\ followed by newline)
        var cleaned = commandLine.Replace("\\\r\n", " ").Replace("\\\n", " ").Replace("\\\r", " ");

        for (int i = 0; i < cleaned.Length; i++)
        {
            char c = cleaned[i];

            if (isEscaped)
            {
                currentToken.Append(c);
                isEscaped = false;
                continue;
            }

            if (c == '\\')
            {
                isEscaped = true;
                continue;
            }

            if (inQuotes)
            {
                if (c == quoteChar)
                {
                    inQuotes = false;
                }
                else
                {
                    currentToken.Append(c);
                }
            }
            else
            {
                if (c == '"' || c == '\'')
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                }
                else
                {
                    currentToken.Append(c);
                }
            }
        }

        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        return tokens;
    }
}
