using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAi.Application.Abstractions.Chunking;
using MyAi.Application.Chunking;
using MyAi.Application.Common.Behaviors;
using MyAi.Application.Configuration;

namespace MyAi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.Configure<DocumentChunkingOptions>(configuration.GetSection(DocumentChunkingOptions.SectionName));
        services.AddSingleton<IDocumentChunker, DocumentChunker>();
        services.AddMediatR(mediatR => mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
