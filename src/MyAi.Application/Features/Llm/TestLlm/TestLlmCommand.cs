using MediatR;

namespace MyAi.Application.Features.Llm.TestLlm;

public sealed record TestLlmCommand(string Prompt) : IRequest<TestLlmResponse>;
