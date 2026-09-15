# AI RAG Application

A Retrieval-Augmented Generation (RAG) document assistant built with **ASP.NET Core 8** and Clean Architecture. The backend ingests PDF/TXT documents, stores OpenAI embeddings in PostgreSQL with **pgvector**, retrieves relevant chunks, and answers questions through a grounded **Chat API**.

An **Angular** chat UI is planned. Persistent conversation history, streaming, and authentication are **not implemented yet**.

---

## What this project does

The application accepts PDF and TXT documents, extracts their text, splits that text into overlapping chunks, generates OpenAI embeddings, and stores those vectors in PostgreSQL using the **pgvector** extension.

A user can then send a natural-language message to **`POST /api/chat`**. The Chat API reuses the existing RAG pipeline: embed the question, search stored chunks, build context, and call the OpenAI LLM. The response is a **grounded answer** plus document **sources** — not an ungrounded model guess.

`POST /api/chat` is **single-turn**. It does not store conversation history.

---

## What is RAG?

**Retrieval-Augmented Generation** combines two steps:

1. **Retrieve** — find document passages that are semantically close to a user question.
2. **Generate** — send those passages to an LLM so the answer is grounded in the uploaded documents instead of model memory alone.

This repository implements ingest, embedding persistence, semantic search, RAG generation, and a Chat API over that RAG pipeline.

---

## Current pipeline

```text
User
    ↓
Chat API
    ↓
CQRS / MediatR
    ↓
RAG Service
    ↓
Query Embedding
    ↓
PostgreSQL + pgvector
    ↓
Relevant Document Chunks
    ↓
RAG Context
    ↓
OpenAI LLM
    ↓
Grounded Answer + Sources
```

Document ingest still runs as a separate pipeline before chat:

```text
Document
    → Text Extraction
    → Chunking
    → OpenAI Embedding
    → PostgreSQL + pgvector
```

| Step | What happens | Document status |
| --- | --- | --- |
| 1. Upload | PDF or TXT (max 10 MB) is stored under `wwwroot/uploads/documents` and a `Documents` row is created | `Uploaded` |
| 2. Extract text | PDF text is read with PdfPig; TXT is read as UTF-8. The full text is saved on the document | `Processing` → `Processed` (or `Failed`) |
| 3. Chunk | Extracted text is split into overlapping word windows (default **1000** words, **150** overlap) and written to `DocumentChunks` | `Processed` |
| 4. Embed | Each chunk is sent to OpenAI `text-embedding-3-small`. The resulting **1536**-dimension vector is stored on `DocumentChunks.Embedding` | `Embedded` (or `Failed`) |
| 5. Semantic search | The question is embedded, then pgvector ranks stored chunks by cosine similarity | Read-only |
| 6. RAG | Retrieved chunk content is built into context and sent to the existing OpenAI chat model | Read-only |
| 7. Chat API | `POST /api/chat` exposes RAG as a chat-oriented request (`message` + `topK`) | Read-only, single-turn |

Re-running embedding generation **updates** the existing chunk rows. It does not insert duplicate chunks or duplicate embedding records.

---

## Architecture

The solution follows **Clean Architecture** with **CQRS** via **MediatR**. Controllers send commands/queries only. Domain and Application contain no PostgreSQL or pgvector types.

| Layer | Project | Responsibility |
| --- | --- | --- |
| API | `src/MyAi.Api` | REST controllers, Swagger, exception middleware, User Secrets |
| Application | `src/MyAi.Application` | Use cases, validators, abstractions (`IEmbeddingService`, `ISemanticSearchService`, `IChatCompletionService`, `IRagService`) |
| Domain | `src/MyAi.Domain` | Entities (`Document`, `DocumentChunk`), statuses |
| Infrastructure | `src/MyAi.Infrastructure` | EF Core, PostgreSQL + pgvector search, OpenAI embeddings and chat, PdfPig, local file storage |
| Tests | `tests/MyAi.Application.Tests` | xUnit coverage for ingest, search, RAG, and chat |

`DocumentChunk.Embedding` is a `float[]?` in Domain. Infrastructure maps it to PostgreSQL `vector(1536)` with `Pgvector.EntityFrameworkCore`.

Chat and RAG are **read-only** against documents. They do not change document or chunk data.

---

## Project structure

```text
MyAIProject/
├── MyAi.slnx
├── src/
│   ├── MyAi.Api/                  # ASP.NET Core host, Swagger, documents, search, RAG, chat
│   ├── MyAi.Application/          # CQRS commands/queries, validators, RAG service
│   ├── MyAi.Domain/               # Entities and domain rules
│   └── MyAi.Infrastructure/       # EF Core, OpenAI, pgvector, PdfPig
└── tests/
    └── MyAi.Application.Tests/    # Automated tests
```

Solution file: `MyAi.slnx`.

---

## Technologies

| Area | Stack |
| --- | --- |
| Language / runtime | C#, .NET 8 |
| API | ASP.NET Core, REST, Swagger / OpenAPI (Swashbuckle) |
| Application style | Clean Architecture, CQRS, MediatR, FluentValidation |
| Persistence | Entity Framework Core 8, PostgreSQL 16, pgvector |
| Embeddings | OpenAI `text-embedding-3-small` (1536 dimensions) |
| Search | pgvector cosine distance (`<=>`); API returns cosine similarity (`1 - distance`) |
| LLM | OpenAI chat (`gpt-4o-mini` by default) via `IChatCompletionService` |
| Text extraction | PdfPig (PDF), UTF-8 (TXT) |
| Planned UI | Angular (not in this repository yet) |

---

## API endpoints

| Method | Path | Description |
| --- | --- | --- |
| `POST` | `/api/Documents` | Upload a PDF or TXT file (`multipart/form-data`) |
| `POST` | `/api/Documents/{id}/extract-text` | Extract text and create/replace chunks |
| `POST` | `/api/Documents/{id}/generate-embeddings` | Generate embeddings and persist them on existing chunks |
| `POST` | `/api/search/semantic` | Semantic similarity search over stored chunk embeddings |
| `POST` | `/api/llm/test` | Direct LLM test (does **not** use document retrieval) |
| `POST` | `/api/rag/query` | RAG: retrieve chunks and generate a grounded answer |
| `POST` | `/api/chat` | Chat-oriented API over the same RAG pipeline (single-turn) |
| `GET` | `/api/Health` | Health check |

Swagger UI is enabled in Development (`launchUrl`: `swagger`). Typical local URLs:

- HTTP: `http://localhost:5235/swagger`
- HTTPS: `https://localhost:7003/swagger`

### RAG vs Chat

| | `POST /api/rag/query` | `POST /api/chat` |
| --- | --- | --- |
| Purpose | RAG use case | Chat-oriented wrapper for Angular / clients |
| Request field | `query` | `message` |
| Pipeline | `IRagService` | Same `IRagService` |
| Response | `query`, `answer`, `sources` | `message`, `answer`, `sources` |
| History | None | None (single-turn) |

Both endpoints retrieve from PostgreSQL + pgvector and answer with the existing OpenAI LLM. Chat does **not** bypass RAG or call the LLM without retrieved context.

---

## Chat API (Step 10) — COMPLETE

`POST /api/chat` exposes RAG as a chat request. `ChatController` is thin: it sends `ChatQuery` through **MediatR**. `ChatQueryHandler` calls reusable **`IRagService` / `RagService`**. It does not duplicate embedding, search, context building, or LLM clients.

Reuse:

- `IEmbeddingService` for the question embedding
- `ISemanticSearchService` for pgvector ranking
- `IChatCompletionService` for the grounded LLM call
- Existing RAG system instructions and context builder
- Existing validation, cancellation, and error handling

`message` is required and must not be empty/whitespace. `topK` must be between **1** and **20** (same limit as semantic search / RAG).

### Request

```json
{
  "message": "What is semantic search?",
  "topK": 5
}
```

### Response (example)

Similarity values vary per query and corpus. The number below is an example only.

```json
{
  "message": "What is semantic search?",
  "answer": "Semantic search focuses on meaning rather than exact keyword matches...",
  "sources": [
    {
      "documentId": 5,
      "chunkId": 39,
      "chunkIndex": 9,
      "fileName": "RAG_Test_Document_25_Pages.txt",
      "similarity": 0.4076
    }
  ]
}
```

There is no `embedding` field. Raw vectors, OpenAI SDK objects, and API keys are not returned.

Unsupported questions must **not** invent facts. If the documents do not contain the answer, the model states that it cannot be determined from the provided documents (or, when no chunks are retrieved, returns that no relevant information was found).

---

## Semantic search (Step 7)

Semantic search ranks stored document chunks by **meaning**, not by exact keyword match.

How it works:

1. Validate the request (`query` required; `topK` between **1** and **20**, default **5**).
2. Embed the question with the existing **`IEmbeddingService`**.
3. Produce a **1536**-dimension query vector (`text-embedding-3-small`).
4. Search `"DocumentChunks"."Embedding"` in PostgreSQL with **pgvector**.
5. Rank by cosine **distance** (`Embedding <=> queryVector`).
6. Return cosine **similarity** as `1 - cosineDistance`, with chunk `Content` and identifiers.
7. Omit the raw 1536-dimension embedding from the API response.

The use case is CQRS: `SearchDocumentChunksQuery` → handler → `ISemanticSearchService`.

### Endpoint

`POST /api/search/semantic`

```json
{
  "query": "What is semantic search?",
  "topK": 5
}
```

---

## PostgreSQL + pgvector

- Database: `MyAiDb` (local PostgreSQL 16)
- Extension: `vector`
- Table / column: `"DocumentChunks"."Embedding"`
- Column type: **`vector(1536)`**

EF Core owns the schema. The `AddDocumentChunkEmbedding` migration enables the `vector` extension and converts the embedding column to `vector(1536)`.

The PostgreSQL server must have the **pgvector** extension files installed before `CREATE EXTENSION vector` can succeed.

### Current database verification

| Check | Result |
| --- | --- |
| Document chunks | 22 |
| Chunks with embeddings | 22 |
| Missing embeddings | 0 |
| Embedding dimension | 1536 |
| PostgreSQL column type | `vector` |
| Re-run embeddings | Updates existing rows; no duplicates |

---

## How embeddings work

1. `GenerateDocumentEmbeddings` loads the document and its chunks.
2. Empty documents, missing chunks, and empty chunk content are rejected.
3. The existing `IEmbeddingService` (`OpenAIEmbeddingService`) calls OpenAI in batches.
4. Each returned vector is checked for length **1536**.
5. The vector is assigned to `DocumentChunk.Embedding` on the **same** chunk row.
6. `SaveChangesAsync` writes the value through EF Core to `vector(1536)`.
7. The document status becomes `Embedded`.

Semantic search, RAG, and Chat **reuse** this same `IEmbeddingService` for the user question. They do not write new chunks or embeddings.

---

## Security and configuration

Secrets are **not** committed. `appsettings.json` may contain non-secret defaults; the OpenAI API key and the PostgreSQL password belong in **User Secrets**.

API project User Secrets ID: `b8efb54a-3850-4bd5-8f1c-486c73759e61`

```powershell
cd src/MyAi.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=MyAiDb;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_OPENAI_API_KEY"
```

Relevant settings (non-secret defaults in `appsettings.json`):

```json
{
  "DocumentChunking": {
    "ChunkSize": 1000,
    "ChunkOverlap": 150
  },
  "OpenAI": {
    "EmbeddingModel": "text-embedding-3-small",
    "EmbeddingDimensions": 1536,
    "ChatModel": "gpt-4o-mini"
  }
}
```

Retrieved document text is treated as **untrusted reference material**, not as system instructions.

---

## Getting started

### Prerequisites

- .NET 8 SDK
- PostgreSQL 16 with the **pgvector** extension installed on the server
- An OpenAI API key

### Database

```powershell
dotnet ef database update --project src/MyAi.Infrastructure --startup-project src/MyAi.Api
```

### Run the API

```powershell
dotnet run --project src/MyAi.Api
```

### Build and test

```powershell
dotnet build MyAi.slnx
dotnet test tests/MyAi.Application.Tests/MyAi.Application.Tests.csproj
```

### Current verification (Step 10)

- Build succeeded: **0 warnings, 0 errors**
- Tests: **69 passed, 0 failed**
- Swagger: `POST /api/chat` is listed and testable

Live Swagger checks:

1. Supported question: “What is semantic search?” — grounded answer plus sources
2. Document question: “Why do we split a large document into smaller pieces before searching it?” — answer related to chunking in the uploaded document
3. Unsupported question: “What is the CEO's favorite food?” — **the answer could not be determined from the provided documents**; no invented fact

---

## Implementation status

### Completed

- [x] Clean Architecture foundation
- [x] Document upload
- [x] PDF/TXT text extraction
- [x] Document chunking
- [x] OpenAI embedding generation
- [x] PostgreSQL integration
- [x] pgvector integration
- [x] Embedding persistence
- [x] Semantic search
- [x] Vector similarity search
- [x] OpenAI LLM integration
- [x] RAG pipeline
- [x] Chat API (`POST /api/chat`)
- [x] Automated tests (69 passed)

### Not implemented yet

- [ ] Angular chat interface
- [ ] Persistent conversation history / chat memory
- [ ] Streaming responses
- [ ] Authentication
- [ ] OCR
- [ ] Vision / image processing
- [ ] AI agent tools
- [ ] Gmail / Calendar integrations
- [ ] Automatic document ingestion pipeline
- [ ] Stripe / subscriptions
- [ ] End-to-end automatic RAG pipeline (upload → extract → chunk → embed in one call)

Chat is single-turn and document-grounded. It is not a multi-turn chatbot with memory or an Angular UI.

---

## Roadmap

1. ~~**RAG context retrieval**~~ — complete (Step 9)
2. ~~**LLM answers**~~ — complete (Step 8–9)
3. ~~**Chat API**~~ — **COMPLETE** (Step 10)
4. **Angular UI** — upload documents and chat against the indexed corpus
5. **Conversation history, streaming, and authentication** — later product features

---

## Author

**Muhammad Atif**

Senior .NET Developer | ASP.NET Core | C# | Angular | AI
