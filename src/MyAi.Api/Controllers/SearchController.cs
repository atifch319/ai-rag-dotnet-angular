using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyAi.Application.Features.Search.SearchDocumentChunks;

namespace MyAi.Api.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController : ControllerBase
{
    private readonly ISender _sender;

    public SearchController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Semantic similarity search over stored document-chunk embeddings.
    /// Returns the top matching chunks ordered by cosine similarity.
    /// Similarity is 1 minus pgvector cosine distance (Embedding &lt;=&gt; query vector).
    /// Embedding vectors are not included in the response.
    /// </summary>
    [HttpPost("semantic")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(SearchDocumentChunksResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchDocumentChunksResponse>> SemanticSearch(
        [FromBody] SearchDocumentChunksQuery request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(response);
    }
}
