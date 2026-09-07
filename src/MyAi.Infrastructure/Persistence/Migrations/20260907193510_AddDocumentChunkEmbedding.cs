using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentChunkEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            // PostgreSQL cannot implicitly cast real[] to vector; keep existing values.
            migrationBuilder.Sql(
                """ALTER TABLE "DocumentChunks" ALTER COLUMN "Embedding" TYPE vector(1536) USING "Embedding"::vector(1536);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """ALTER TABLE "DocumentChunks" ALTER COLUMN "Embedding" TYPE real[] USING "Embedding"::real[];""");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
