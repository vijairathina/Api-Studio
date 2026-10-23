using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ApiStudio.Application.Abstractions;
using ApiStudio.Domain.Entities;
using ApiStudio.Domain.Enums;
using ApiStudio.Domain.ValueObjects;

namespace ApiStudio.Infrastructure.Http;

public class HttpClientRequestExecutor : IHttpRequestExecutor
{
    private static readonly HashSet<string> ContentHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Allow",
        "Content-Disposition",
        "Content-Encoding",
        "Content-Language",
        "Content-Length",
        "Content-Location",
        "Content-MD5",
        "Content-Range",
        "Content-Type",
        "Expires",
        "Last-Modified"
    };

    public async Task<ApiResponse> ExecuteAsync(ApiRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate URL
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return ApiResponse.Failure("Please specify a request URL.", 0);
            }

            var fullUrl = BuildFullUrl(request.Url, request.QueryParams, request.Auth);

            if (!Uri.TryCreate(fullUrl, UriKind.Absolute, out var uri) || 
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return ApiResponse.Failure("Please check the request URL. It must be a valid http:// or https:// address.", 0);
            }

            // Create handler with redirection & cookie support
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = request.FollowRedirects,
                MaxAutomaticRedirections = 10,
                AutomaticDecompression = DecompressionMethods.All,
                CookieContainer = new CookieContainer()
            };

            using var httpClient = new HttpClient(handler, disposeHandler: true);
            var timeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 30;
            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var httpMethod = ResolveHttpMethod(request);
            using var httpRequest = new HttpRequestMessage(httpMethod, uri);

            // Setup Request Body
            HttpContent? httpContent = CreateHttpContent(request.Body);
            if (httpContent != null)
            {
                httpRequest.Content = httpContent;
            }

            // Apply Headers
            ApplyHeaders(httpRequest, request.Headers, httpContent);

            // Apply Authorization
            ApplyAuthorization(httpRequest, request.Auth);

            // Execute HTTP Request
            using var httpResponse = await httpClient.SendAsync(
                httpRequest, 
                HttpCompletionOption.ResponseHeadersRead, 
                cts.Token).ConfigureAwait(false);

            var bodyBytes = await httpResponse.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
            var bodyText = Encoding.UTF8.GetString(bodyBytes);
            stopwatch.Stop();

            var response = new ApiResponse
            {
                StatusCode = (int)httpResponse.StatusCode,
                StatusDescription = httpResponse.ReasonPhrase ?? httpResponse.StatusCode.ToString(),
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ContentSizeBytes = bodyBytes.Length,
                Body = bodyText,
                ContentType = httpResponse.Content.Headers.ContentType?.ToString() ?? "text/plain",
                CompletedAt = DateTime.UtcNow
            };

            // Capture response headers
            foreach (var h in httpResponse.Headers)
            {
                response.Headers.Add(new KeyValuePair<string, string>(h.Key, string.Join(", ", h.Value)));
            }
            foreach (var h in httpResponse.Content.Headers)
            {
                response.Headers.Add(new KeyValuePair<string, string>(h.Key, string.Join(", ", h.Value)));
            }

            // Capture cookies
            if (httpResponse.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                response.Cookies.AddRange(setCookies);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return ApiResponse.Failure("The request was canceled.", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) // Timeout
        {
            stopwatch.Stop();
            return ApiResponse.Failure("The request timed out.", stopwatch.ElapsedMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var friendlyMessage = FormatHttpRequestException(ex);
            return ApiResponse.Failure(friendlyMessage, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ApiResponse.Failure($"Request failed: {ex.Message}", stopwatch.ElapsedMilliseconds);
        }
    }

    private static string BuildFullUrl(string rawUrl, IEnumerable<KeyValuePairItem> queryParams, AuthSettings? auth)
    {
        var urlBuilder = new StringBuilder(rawUrl.Trim());

        var activeParams = queryParams.Where(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key)).ToList();

        // Check if API key is in query params
        if (auth != null && auth.Type == AuthType.ApiKey && 
            auth.ApiKeyLocation == ApiKeyLocation.QueryParam && 
            !string.IsNullOrWhiteSpace(auth.ApiKeyName))
        {
            activeParams.Add(new KeyValuePairItem(auth.ApiKeyName, auth.ApiKeyValue));
        }

        if (activeParams.Count > 0)
        {
            var separator = rawUrl.Contains('?') ? '&' : '?';
            for (int i = 0; i < activeParams.Count; i++)
            {
                var p = activeParams[i];
                urlBuilder.Append(i == 0 ? separator : '&');
                urlBuilder.Append(Uri.EscapeDataString(p.Key));
                urlBuilder.Append('=');
                urlBuilder.Append(Uri.EscapeDataString(p.Value));
            }
        }

        return urlBuilder.ToString();
    }

    private static System.Net.Http.HttpMethod ResolveHttpMethod(ApiRequest request)
    {
        return request.Method switch
        {
            HttpMethodType.GET => System.Net.Http.HttpMethod.Get,
            HttpMethodType.POST => System.Net.Http.HttpMethod.Post,
            HttpMethodType.PUT => System.Net.Http.HttpMethod.Put,
            HttpMethodType.PATCH => System.Net.Http.HttpMethod.Patch,
            HttpMethodType.DELETE => System.Net.Http.HttpMethod.Delete,
            HttpMethodType.HEAD => System.Net.Http.HttpMethod.Head,
            HttpMethodType.OPTIONS => System.Net.Http.HttpMethod.Options,
            HttpMethodType.CUSTOM => new System.Net.Http.HttpMethod(
                string.IsNullOrWhiteSpace(request.CustomMethod) ? "CUSTOM" : request.CustomMethod.Trim().ToUpperInvariant()),
            _ => System.Net.Http.HttpMethod.Get
        };
    }

    private static HttpContent? CreateHttpContent(RequestBodySettings? body)
    {
        if (body == null || body.Type == BodyType.None)
        {
            return null;
        }

        switch (body.Type)
        {
            case BodyType.Json:
                return new StringContent(body.RawContent ?? string.Empty, Encoding.UTF8, "application/json");

            case BodyType.Xml:
                return new StringContent(body.RawContent ?? string.Empty, Encoding.UTF8, "application/xml");

            case BodyType.Text:
                return new StringContent(body.RawContent ?? string.Empty, Encoding.UTF8, "text/plain");

            case BodyType.FormUrlEncoded:
                var formValues = body.UrlEncodedData
                    .Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.Key))
                    .Select(x => new KeyValuePair<string, string>(x.Key, x.Value))
                    .ToList();
                return new FormUrlEncodedContent(formValues);

            case BodyType.MultipartFormData:
                var multipart = new MultipartFormDataContent();
                foreach (var item in body.FormData.Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.Key)))
                {
                    multipart.Add(new StringContent(item.Value), item.Key);
                }
                return multipart;

            case BodyType.BinaryFile when !string.IsNullOrWhiteSpace(body.BinaryFilePath) && File.Exists(body.BinaryFilePath):
                var fileBytes = File.ReadAllBytes(body.BinaryFilePath);
                var byteContent = new ByteArrayContent(fileBytes);
                byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                return byteContent;

            default:
                return null;
        }
    }

    private static void ApplyHeaders(HttpRequestMessage request, IEnumerable<KeyValuePairItem> headers, HttpContent? content)
    {
        foreach (var header in headers.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key)))
        {
            var key = header.Key.Trim();
            var val = header.Value.Trim();

            if (ContentHeaderNames.Contains(key))
            {
                content?.Headers.TryAddWithoutValidation(key, val);
            }
            else
            {
                request.Headers.TryAddWithoutValidation(key, val);
            }
        }
    }

    private static void ApplyAuthorization(HttpRequestMessage request, AuthSettings? auth)
    {
        if (auth == null || auth.Type == AuthType.None)
        {
            return;
        }

        switch (auth.Type)
        {
            case AuthType.Bearer when !string.IsNullOrWhiteSpace(auth.BearerToken):
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken.Trim());
                break;

            case AuthType.Basic:
                var credentials = $"{auth.BasicUsername}:{auth.BasicPassword}";
                var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64);
                break;

            case AuthType.ApiKey when auth.ApiKeyLocation == ApiKeyLocation.Header && !string.IsNullOrWhiteSpace(auth.ApiKeyName):
                request.Headers.TryAddWithoutValidation(auth.ApiKeyName, auth.ApiKeyValue);
                break;
        }
    }

    private static string FormatHttpRequestException(HttpRequestException ex)
    {
        if (ex.InnerException is System.Security.Authentication.AuthenticationException)
        {
            return "A secure TLS/SSL connection could not be established.";
        }

        if (ex.StatusCode.HasValue)
        {
            return $"Server responded with error status code {(int)ex.StatusCode.Value} ({ex.StatusCode.Value}).";
        }

        var message = ex.Message.ToLowerInvariant();
        if (message.Contains("refused") || message.Contains("actively refused"))
        {
            return "Unable to connect to the server. The connection was refused.";
        }
        if (message.Contains("host") || message.Contains("name resolution") || message.Contains("dns"))
        {
            return "Unable to resolve host. Please verify the host name in the URL.";
        }

        return "Unable to connect to the server.";
    }
}
