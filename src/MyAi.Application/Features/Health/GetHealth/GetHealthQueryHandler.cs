using MediatR;

namespace MyAi.Application.Features.Health.GetHealth;

public sealed class GetHealthQueryHandler : IRequestHandler<GetHealthQuery, GetHealthResponse>
{
    public Task<GetHealthResponse> Handle(GetHealthQuery request, CancellationToken cancellationToken)
    {
        var response = new GetHealthResponse(
            Status: "Healthy",
            Layer: "Application",
            TimestampUtc: DateTimeOffset.UtcNow);

        return Task.FromResult(response);
    }
}
