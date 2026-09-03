using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MyAi.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var apiPath = ResolveApiProjectPath();

        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables();

        var userSecretsId = ReadUserSecretsId(apiPath);
        if (!string.IsNullOrWhiteSpace(userSecretsId))
        {
            configurationBuilder.AddUserSecrets(userSecretsId);
        }

        var configuration = configurationBuilder.Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found. Set it in User Secrets or environment variables.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveApiProjectPath()
    {
        var current = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(current, "src", "MyAi.Api"),
            Path.Combine(current, "..", "MyAi.Api"),
            Path.Combine(current, "MyAi.Api")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(fullPath, "appsettings.json")))
            {
                return fullPath;
            }
        }

        throw new InvalidOperationException("Could not locate the MyAi.Api project for design-time configuration.");
    }

    private static string? ReadUserSecretsId(string apiPath)
    {
        var csprojPath = Path.Combine(apiPath, "MyAi.Api.csproj");
        if (!File.Exists(csprojPath))
        {
            return null;
        }

        var match = Regex.Match(
            File.ReadAllText(csprojPath),
            @"<UserSecretsId>([^<]+)</UserSecretsId>");

        return match.Success ? match.Groups[1].Value : null;
    }
}
