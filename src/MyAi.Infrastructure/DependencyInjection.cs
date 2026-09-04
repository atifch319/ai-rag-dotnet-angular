using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAi.Application.Abstractions.Persistence;
using MyAi.Application.Abstractions.Storage;
using MyAi.Application.Abstractions.TextExtraction;
using MyAi.Domain.Interfaces;
using MyAi.Infrastructure.Persistence;
using MyAi.Infrastructure.Persistence.Repositories;
using MyAi.Infrastructure.Storage;
using MyAi.Infrastructure.TextExtraction;

namespace MyAi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IDocumentStorageService, LocalDocumentStorageService>();
        services.AddScoped<ITextExtractor, TextExtractor>();

        return services;
    }
}
