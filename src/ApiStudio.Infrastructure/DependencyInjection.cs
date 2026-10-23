using ApiStudio.Application.Abstractions;
using ApiStudio.Application.Services;
using ApiStudio.Infrastructure.Http;
using ApiStudio.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ApiStudio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddApiStudioServices(this IServiceCollection services, string? dataDirectory = null)
    {
        // Core HTTP & Execution Services
        services.AddSingleton<IHttpRequestExecutor, HttpClientRequestExecutor>();
        services.AddSingleton<IVariableResolver, VariableResolver>();
        services.AddSingleton<IAssertionEvaluator, AssertionEvaluator>();
        services.AddSingleton<ICurlConverter, CurlConverter>();
        services.AddSingleton<ICollectionRunner, CollectionRunner>();
        services.AddSingleton<IImportExportService, ImportExportService>();

        // Persistence Repositories
        services.AddSingleton<ICollectionRepository>(_ => new JsonFileCollectionRepository(dataDirectory));
        services.AddSingleton<IEnvironmentRepository>(_ => new JsonFileEnvironmentRepository(dataDirectory));
        services.AddSingleton<IHistoryRepository>(_ => new JsonFileHistoryRepository(dataDirectory));
        services.AddSingleton<IUserPreferencesRepository>(_ => new JsonFilePreferencesRepository(dataDirectory));

        return services;
    }
}
