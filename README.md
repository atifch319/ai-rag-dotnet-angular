# AI RAG Application

A Retrieval-Augmented Generation (RAG) document assistant built with **ASP.NET Core 8** and Clean Architecture. The backend can ingest documents, store OpenAI embeddings in PostgreSQL with **pgvector**, and run **semantic similarity search** over those vectors.

An **Angular** frontend is planned. RAG context assembly, LLM answer generation, a Chat API, and a chat UI are **not implemented yet**.

---

## What this project does

The application accepts PDF and TXT documents, extracts their text, splits that text into overlapping chunks, generates OpenAI embeddings, and stores those vectors in PostgreSQL using the **pgvector** extension.

A user can then ask a natural-language question. The API embeds that question with the same OpenAI model and ranks stored `DocumentChunks` by vector similarity. The response is the most relevant **chunk content** and a similarity score — not a generated answer from a language model.

---

## What is RAG?

**Retrieval-Augmented Generation** combines two steps:

1. **Retrieve** — find document passages that are semantically close to a user question.
2. **Generate** — send those passages to an LLM so the answer is grounded in the uploaded documents instead of model memory alone.

This repository currently implements document ingest, embedding persistence, and **semantic retrieval of chunks**. It does **not** yet assemble RAG context for a prompt, call an LLM, or expose a chat API.

---

## Current pipeline

```text
Document
    → Text Extraction
    → Chunking
    → OpenAI Embedding
    → PostgreSQL + pgvector
    → User Question Embedding
    → Vector Similarity Search
    → Relevant Document Chunks
```

Step 7 returns relevant `Content`. It does **not** send that content to an LLM.

| Step | What happens | Document status |
| --- | --- | --- |
| 1. Upload | PDF or TXT (max 10 MB) is stored under `wwwroot/uploads/documents` and a `Documents` row is created | `Uploaded` |
| 2. Extract text | PDF text is read with PdfPig; TXT is read as UTF-8. The full text is saved on the document | `Processing` → `Processed` (or `Failed`) |
| 3. Chunk | Extracted text is split into overlapping word windows (default **1000** words, **150** overlap) and written to `DocumentChunks` | `Processed` |
| 4. Embed | Each chunk is sent to OpenAI `text-embedding-3-small`. The resulting **1536**-dimension vector is stored on `DocumentChunks.Embedding` | `Embedded` (or `Failed`) |
| 5. Semantic search | The user question is embedded with the same service, then pgvector ranks stored chunks by cosine similarity | Read-only (documents are not modified) |

Re-running embedding generation **updates** the existing chunk rows. It does not insert duplicate chunks or duplicate embedding records.

---

## Architecture

The solution follows **Clean Architecture** with **CQRS** via **MediatR**. Controllers send commands/queries only. Domain and Application contain no PostgreSQL or pgvector types.

| Layer | Project | Responsibility |
| --- | --- | --- |
| API | `src/MyAi.Api` | REST controllers, Swagger, exception middleware, User Secrets |
| Application | `src/MyAi.Application` | Use cases, validators, abstractions (`IEmbeddingService`, `ISemanticSearchService`, `ITextExtractor`, `IDocumentChunker`) |
| Domain | `src/MyAi.Domain` | Entities (`Document`, `DocumentChunk`), statuses |
| Infrastructure | `src/MyAi.Infrastructure` | EF Core, PostgreSQL + pgvector search, OpenAI, PdfPig, local file storage |
| Tests | `tests/MyAi.Application.Tests` | xUnit coverage for chunking, embeddings, and semantic search |

`DocumentChunk.Embedding` is a `float[]?` in Domain. Infrastructure maps it to PostgreSQL `vector(1536)` with `Pgvector.EntityFrameworkCore`.

Semantic search is a **read-only query**. It does not change document or chunk data.

---

## Project structure

```text
MyAIProject/
├── MyAi.slnx
├── src/
│   ├── MyAi.Api/                  # ASP.NET Core host, Swagger, uploads, search endpoint
│   ├── MyAi.Application/          # CQRS commands/queries, validators, chunking
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
| `GET` | `/api/Health` | Health check |

Swagger UI is enabled in Development (`launchUrl`: `swagger`). Typical local URLs:

- HTTP: `http://localhost:5235/swagger`
- HTTPS: `https://localhost:7003/swagger`

There is no Chat API and no LLM answer endpoint.

---

## Semantic search (Step 7)

Semantic search ranks stored document chunks by **meaning**, not by exact keyword match.

How it works:

1. Validate the request (`query` required; `topK` between **1** and **20**, default **5**).
2. Embed the question with the existing **`IEmbeddingService`** (no second embedding service).
3. Produce a **1536**-dimension query vector (`text-embedding-3-small`).
4. Search `"DocumentChunks"."Embedding"` in PostgreSQL with **pgvector**.
5. Rank by cosine **distance** (`Embedding <=> queryVector`).
6. Return cosine **similarity** as `1 - cosineDistance`, with chunk `Content` and identifiers.
7. Omit the raw 1536-dimension embedding from the API response.

Chunks with a null embedding are excluded. If nothing is indexed, the API returns an empty `results` array. Empty queries, invalid `topK`, OpenAI failures, and vector-search failures return a validation error. `CancellationToken` is honored throughout.

The use case is CQRS: `SearchDocumentChunksQuery` → handler → `ISemanticSearchService` (Infrastructure `PgvectorSemanticSearchService`).

### Endpoint

`POST /api/search/semantic`

```json
{
  "query": "What is semantic search?",
  "topK": 5
}
```

Response fields (camelCase JSON): `query`, `topK`, `resultCount`, and `results[]` with `chunkId`, `documentId`, `chunkIndex`, `content`, and `similarity`. There is no `embedding` field.

---

## Semantic Search Example

User question:

> How can a system find information based on meaning instead of matching exact words?

The application:

1. Generates an embedding for the question (same OpenAI embedding model as document chunks).
2. Searches stored `DocumentChunks` embeddings in PostgreSQL.
3. Uses pgvector similarity ranking (cosine distance, exposed as similarity).
4. Returns the most relevant chunks, ordered by similarity.
5. Returns `Content` and similarity information, **not** the raw embedding vector.

Example request used in verification:

```json
{
  "query": "How can a system find information based on meaning instead of matching exact words?",
  "topK": 10
}
```

That search returned Semantic Search–related chunks near the top of the results.

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

Semantic search **reuses** this same `IEmbeddingService` for the user question. It does not write new chunks or embeddings.

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

### Current verification (Step 7)

- Build succeeded: **0 warnings, 0 errors**
- Tests: **32 passed, 0 failed**
- Live PostgreSQL + pgvector ranking test passed
- Query embedding generation passed
- Results ordered by similarity passed
- Embedding vectors are not exposed in the API response
- Empty query validation passed
- Invalid TopK validation passed

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
- [x] Semantic search automated tests

### Not implemented yet

- [ ] RAG context retrieval
- [ ] LLM integration
- [ ] Chat API
- [ ] Angular chat interface
- [ ] Conversation history
- [ ] Streaming responses
- [ ] Agentic AI
- [ ] End-to-end automatic RAG pipeline

This is not a chatbot yet. You can upload documents, embed them, and retrieve relevant chunks. The API does not generate an LLM answer from those chunks.

---

## Roadmap

1. **RAG context retrieval** — package the top matching chunks as prompt context.
2. **LLM answers** — send retrieved context to a chat model with citations.
3. **Chat API** — conversation endpoint on the ASP.NET Core API.
4. **Angular UI** — upload documents and ask questions against the indexed corpus.
5. **Conversation history, streaming, and agentic workflows** — later product features.

---

## Author

**Muhammad Atif**

Senior .NET Developer | ASP.NET Core | C# | Angular | AI
