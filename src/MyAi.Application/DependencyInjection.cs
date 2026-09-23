using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAi.Application.Abstractions.Chunking;
using MyAi.Application.Abstractions.Rag;
using MyAi.Application.Chunking;
using MyAi.Application.Common.Behaviors;
using MyAi.Application.Configuration;
using MyAi.Application.Features.Rag;

namespace MyAi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.Configure<DocumentChunkingOptions>(configuration.GetSection(DocumentChunkingOptions.SectionName));
        services.AddOptions<RagOptions>()
            .Bind(configuration.GetSection(RagOptions.SectionName))
            .Validate(
                options => options.MinimumSimilarity is >= 0d and <= 1d,
                "Rag:MinimumSimilarity must be between 0 and 1.")
            .ValidateOnStart();
        services.AddSingleton<IDocumentChunker, DocumentChunker>();
        services.AddScoped<IRagService, RagService>();
        services.AddMediatR(mediatR => mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
