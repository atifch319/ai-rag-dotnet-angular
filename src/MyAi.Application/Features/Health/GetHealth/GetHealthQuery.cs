using MediatR;

namespace MyAi.Application.Features.Health.GetHealth;

public sealed record GetHealthQuery : IRequest<GetHealthResponse>;
