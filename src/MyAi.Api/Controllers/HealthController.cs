using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyAi.Application.Features.Health.GetHealth;

namespace MyAi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    private readonly ISender _sender;

    public HealthController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<GetHealthResponse>> Get(CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new GetHealthQuery(), cancellationToken);
        return Ok(response);
    }
}
