using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyAi.Application.Features.Documents.ExtractText;
using MyAi.Application.Features.Documents.Upload;

namespace MyAi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DocumentsController : ControllerBase
{
    private readonly ISender _sender;

    public DocumentsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Upload a PDF or TXT document.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        var command = new UploadDocumentCommand(file);
        var response = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Upload), new { id = response.Id }, response);
    }

    /// <summary>
    /// Extract text from an uploaded PDF or TXT document.
    /// </summary>
    [HttpPost("{id:long}/extract-text")]
    [ProducesResponseType(typeof(ExtractDocumentTextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExtractDocumentTextResponse>> ExtractText(
        long id,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new ExtractDocumentTextCommand(id), cancellationToken);
        return Ok(response);
    }
}

