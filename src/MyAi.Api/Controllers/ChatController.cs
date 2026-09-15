using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyAi.Application.Features.Chat.SendChatMessage;

namespace MyAi.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private readonly ISender _sender;

    public ChatController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Chat over uploaded documents using the existing RAG pipeline.
    /// Returns a grounded answer and sources. Does not persist conversation history.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponse>> Send(
        [FromBody] ChatQuery request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(response);
    }
}
