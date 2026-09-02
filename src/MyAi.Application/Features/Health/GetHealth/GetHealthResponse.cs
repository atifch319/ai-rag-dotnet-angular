namespace MyAi.Application.Features.Health.GetHealth;

public sealed record GetHealthResponse(string Status, string Layer, DateTimeOffset TimestampUtc);
