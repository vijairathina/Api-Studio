using ApiStudio.Application.Abstractions;
using ApiStudio.Domain.Entities;

namespace ApiStudio.Application.Services;

public class CollectionRunner : ICollectionRunner
{
    private readonly IHttpRequestExecutor _requestExecutor;
    private readonly IVariableResolver _variableResolver;
    private readonly IAssertionEvaluator _assertionEvaluator;

    public CollectionRunner(
        IHttpRequestExecutor requestExecutor,
        IVariableResolver variableResolver,
        IAssertionEvaluator assertionEvaluator)
    {
        _requestExecutor = requestExecutor;
        _variableResolver = variableResolver;
        _assertionEvaluator = assertionEvaluator;
    }

    public async Task<CollectionRunResult> RunAsync(
        ApiCollection collection,
        IDictionary<string, string> variables,
        Action<SingleRequestRunResult>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var runResult = new CollectionRunResult
        {
            CollectionId = collection.Id,
            CollectionName = collection.Name,
            ExecutedAt = DateTime.UtcNow
        };

        var allRequests = FlattenRequests(collection);

        foreach (var req in allRequests)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var resolvedReq = _variableResolver.ResolveRequest(req, variables);

            var singleResult = new SingleRequestRunResult
            {
                RequestId = req.Id,
                RequestName = req.Name,
                Method = req.EffectiveMethod,
                Url = resolvedReq.Url
            };

            try
            {
                var response = await _requestExecutor.ExecuteAsync(resolvedReq, cancellationToken);
                singleResult.StatusCode = response.StatusCode;
                singleResult.ElapsedMilliseconds = response.ElapsedMilliseconds;
                singleResult.IsSuccess = response.IsSuccess;
                singleResult.ErrorMessage = response.ErrorMessage;

                if (req.Assertions.Count > 0)
                {
                    singleResult.AssertionResults = _assertionEvaluator.Evaluate(response, req.Assertions);
                }
            }
            catch (Exception ex)
            {
                singleResult.IsSuccess = false;
                singleResult.ErrorMessage = ex.Message;
            }

            runResult.Results.Add(singleResult);
            onProgress?.Invoke(singleResult);
        }

        return runResult;
    }

    private static List<ApiRequest> FlattenRequests(ApiCollection collection)
    {
        var list = new List<ApiRequest>(collection.Requests);

        void ExtractFromFolder(RequestFolder folder)
        {
            list.AddRange(folder.Requests);
            foreach (var sub in folder.SubFolders)
            {
                ExtractFromFolder(sub);
            }
        }

        foreach (var folder in collection.Folders)
        {
            ExtractFromFolder(folder);
        }

        return list;
    }
}
