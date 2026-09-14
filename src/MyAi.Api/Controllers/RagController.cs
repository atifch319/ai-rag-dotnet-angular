using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyAi.Application.Features.Rag.AskRag;

namespace MyAi.Api.Controllers;

[ApiController]
[Route("api/rag")]
public sealed class RagController : ControllerBase
{
    private readonly ISender _sender;

    public RagController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Retrieval-augmented generation: embed the question, search stored chunks with pgvector,
    /// and answer using only the retrieved document context.
    /// Embedding vectors are not included in the response.
    /// </summary>
    [HttpPost("query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AskRagResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AskRagResponse>> Query(
        [FromBody] AskRagQuery request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(response);
    }
}
