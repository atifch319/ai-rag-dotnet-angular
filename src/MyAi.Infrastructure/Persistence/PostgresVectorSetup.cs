using Microsoft.EntityFrameworkCore;
using MyAi.Application.Configuration;
using Npgsql;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace MyAi.Infrastructure.Persistence;

internal static class PostgresVectorSetup
{
    public const int EmbeddingDimensions = 1536;

    public const string EmbeddingColumnType = "vector(1536)";

    static PostgresVectorSetup()
    {
        if (EmbeddingDimensions != new OpenAIOptions().EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                "pgvector column dimensions must match OpenAIOptions.EmbeddingDimensions.");
        }
    }

    public static DbContextOptionsBuilder UseMyAiPostgres(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();

        return optionsBuilder.UseNpgsql(dataSourceBuilder.Build(), npgsql => npgsql.UseVector());
    }
}
