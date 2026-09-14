using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyAi.Application.Features.Llm.TestLlm;

namespace MyAi.Api.Controllers;

[ApiController]
[Route("api/llm")]
public sealed class LlmController : ControllerBase
{
    private readonly ISender _sender;

    public LlmController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Sends a prompt to the configured OpenAI chat model. Used to verify LLM integration.
    /// Does not perform RAG retrieval or use document chunks.
    /// </summary>
    [HttpPost("test")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(TestLlmResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TestLlmResponse>> Test(
        [FromBody] TestLlmCommand request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(response);
    }
}
