# AI RAG Application

A Retrieval-Augmented Generation (RAG) document assistant built with **ASP.NET Core 8** and Clean Architecture. The backend already prepares documents for RAG: upload, text extraction, chunking, OpenAI embeddings, and PostgreSQL + pgvector storage.

An **Angular** frontend is planned. Semantic search, RAG retrieval, LLM answers, and a chat UI are **not implemented yet**.

---

## What this project does

The application accepts PDF and TXT documents, extracts their text, splits that text into overlapping chunks, generates OpenAI embeddings, and stores those vectors in PostgreSQL using the **pgvector** extension.

That stored vector index is the foundation for later RAG features: retrieve the most relevant chunks for a question and send them to a language model. Those retrieval and answer steps are still on the roadmap.

---

## What is RAG?

**Retrieval-Augmented Generation** combines two steps:

1. **Retrieve** — find document passages that are semantically close to a user question.
2. **Generate** — send those passages to an LLM so the answer is grounded in the uploaded documents instead of model memory alone.

This repository currently completes the **ingest and embed** half of RAG. It does **not** yet search vectors, retrieve context, or generate answers.

---

## Current document-processing pipeline

```text
Upload PDF/TXT
    → Extract text
    → Create overlapping document chunks
    → Generate OpenAI embeddings
    → Persist vectors in PostgreSQL (pgvector)
```

| Step | What happens | Document status |
| --- | --- | --- |
| 1. Upload | PDF or TXT (max 10 MB) is stored under `wwwroot/uploads/documents` and a `Documents` row is created | `Uploaded` |
| 2. Extract text | PDF text is read with PdfPig; TXT is read as UTF-8. The full text is saved on the document | `Processing` → `Processed` (or `Failed`) |
| 3. Chunk | Extracted text is split into overlapping word windows (default **1000** words, **150** overlap) and written to `DocumentChunks` | `Processed` |
| 4. Embed | Each chunk is sent to OpenAI `text-embedding-3-small`. The resulting **1536**-dimension vector is stored on `DocumentChunks.Embedding` | `Embedded` (or `Failed`) |

Re-running embedding generation **updates** the existing chunk rows. It does not insert duplicate chunks or duplicate embedding records.

---

## Architecture

The solution follows **Clean Architecture** with **CQRS** via **MediatR**. Controllers send commands/queries only. Domain and Application contain no PostgreSQL or pgvector types.

| Layer | Project | Responsibility |
| --- | --- | --- |
| API | `src/MyAi.Api` | REST controllers, Swagger, exception middleware, User Secrets |
| Application | `src/MyAi.Application` | Use cases, validators, abstractions (`IEmbeddingService`, `ITextExtractor`, `IDocumentChunker`) |
| Domain | `src/MyAi.Domain` | Entities (`Document`, `DocumentChunk`), statuses |
| Infrastructure | `src/MyAi.Infrastructure` | EF Core, PostgreSQL + pgvector, OpenAI, PdfPig, local file storage |
| Tests | `tests/MyAi.Application.Tests` | xUnit coverage for chunking and embedding workflows |

`DocumentChunk.Embedding` is a `float[]?` in Domain. Infrastructure maps it to PostgreSQL `vector(1536)` with `Pgvector.EntityFrameworkCore`.

---

## Project structure

```text
MyAIProject/
├── MyAi.slnx
├── src/
│   ├── MyAi.Api/                  # ASP.NET Core host, Swagger, uploads
│   ├── MyAi.Application/          # CQRS commands, validators, chunking
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
| Text extraction | PdfPig (PDF), UTF-8 (TXT) |
| Planned UI | Angular (not in this repository yet) |

---

## API endpoints

| Method | Path | Description |
| --- | --- | --- |
| `POST` | `/api/Documents` | Upload a PDF or TXT file (`multipart/form-data`) |
| `POST` | `/api/Documents/{id}/extract-text` | Extract text and create/replace chunks |
| `POST` | `/api/Documents/{id}/generate-embeddings` | Generate embeddings and persist them on existing chunks |
| `GET` | `/api/Health` | Health check |

Swagger UI is enabled in Development (`launchUrl`: `swagger`). Typical local URLs:

- HTTP: `http://localhost:5235/swagger`
- HTTPS: `https://localhost:7003/swagger`

There is no chat API and no search endpoint.

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

Rerunning the command overwrites `Embedding` on those rows. The unique index on `(DocumentId, ChunkIndex)` prevents duplicate chunks.

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
    "EmbeddingDimensions": 1536
  }
}
```

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

Current verification:

- Build: **0 warnings, 0 errors**
- Tests: **17 passed, 0 failed**

---

## Implementation status

### Completed

- [x] Clean Architecture (.NET backend)
- [x] Document upload for PDF / TXT
- [x] PDF / TXT text extraction
- [x] Document chunking
- [x] OpenAI embedding generation
- [x] PostgreSQL + pgvector
- [x] Embedding persistence on `DocumentChunks.Embedding`
- [x] EF Core migrations
- [x] Automated tests
- [x] Swagger / OpenAPI
- [x] User Secrets for OpenAI and the database password

### Not implemented yet

- [ ] Semantic search / cosine similarity queries
- [ ] RAG retrieval
- [ ] LLM answer generation
- [ ] Chat API
- [ ] Angular application and chat UI

Do not treat this repository as a working Q&A chatbot. Documents can be uploaded, chunked, embedded, and stored. Questions cannot be answered yet.

---

## Roadmap

1. **Semantic search** — query `DocumentChunks.Embedding` with pgvector similarity.
2. **RAG retrieval** — return the top-k chunks for a question.
3. **LLM answers** — send retrieved context to a chat model with citations.
4. **Chat API** — conversation endpoint on the ASP.NET Core API.
5. **Angular UI** — upload documents and ask questions against the indexed corpus.

---

## Author

**Muhammad Atif**

Senior .NET Developer | ASP.NET Core | C# | Angular | AI
