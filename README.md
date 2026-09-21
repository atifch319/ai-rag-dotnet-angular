# AI-Powered RAG Document Q&A System

A Retrieval-Augmented Generation (RAG) document assistant built with **ASP.NET Core 8**, **PostgreSQL + pgvector**, **OpenAI**, and an **Angular** chat UI.

The backend ingests PDF/TXT documents, stores OpenAI embeddings, retrieves relevant chunks, and answers questions through a grounded **Chat API**. The Angular app provides a chat interface over `POST /api/chat`.

Repository: [https://github.com/atifch319/ai-rag-dotnet-angular](https://github.com/atifch319/ai-rag-dotnet-angular)

Persistent conversation history, streaming, authentication, OCR, and AI agents are **not implemented**.

---

## Project Overview

This is an AI-powered document question-answering application using RAG.

The system:

- Uploads PDF and TXT documents
- Extracts text
- Splits documents into overlapping chunks
- Generates OpenAI embeddings
- Stores embeddings in PostgreSQL using **pgvector**
- Performs semantic search
- Retrieves relevant document chunks
- Builds grounded RAG context
- Uses an OpenAI LLM to generate answers
- Provides an Angular chat UI
- Displays source documents, chunks, and similarity scores
- Instructs the model to refuse an answer when the information cannot be determined from the indexed documents

A user sends a natural-language message to **`POST /api/chat`**. The Chat API reuses the existing RAG pipeline: embed the question, search stored chunks, build context, and call the OpenAI LLM. The response is a **grounded answer** plus document **sources**.

`POST /api/chat` is **single-turn**. It does not store conversation history.

---

## What is RAG?

**Retrieval-Augmented Generation** combines two steps:

1. **Retrieve** — find document passages that are semantically close to a user question.
2. **Generate** — send those passages to an LLM so the answer is grounded in the uploaded documents instead of model memory alone.

This repository implements ingest, embedding persistence, semantic search, RAG generation, a Chat API, and an Angular UI over that pipeline.

---

## Architecture

```text
Angular Chat UI
        ↓
ASP.NET Core Web API
        ↓
Application Layer
        ↓
RAG Service
        ↓
Embedding + Semantic Search
        ↓
PostgreSQL + pgvector
        ↓
Relevant Document Chunks
        ↓
OpenAI LLM
        ↓
Grounded Response
        ↓
Angular UI
```

### Clean Architecture

```text
API
 ↓
Application
 ↓
Domain
```

**Infrastructure** implements Application abstractions. Controllers send commands/queries only. Domain and Application contain no PostgreSQL or pgvector types.

| Layer | Project | Responsibility |
| --- | --- | --- |
| API | `src/MyAi.Api` | REST controllers, Swagger, CORS (Development), exception middleware, User Secrets |
| Application | `src/MyAi.Application` | Use cases, validators, abstractions (`IEmbeddingService`, `ISemanticSearchService`, `IChatCompletionService`, `IRagService`) |
| Domain | `src/MyAi.Domain` | Entities (`Document`, `DocumentChunk`), statuses |
| Infrastructure | `src/MyAi.Infrastructure` | EF Core, PostgreSQL + pgvector search, OpenAI embeddings and chat, PdfPig, local file storage |
| Tests | `tests/MyAi.Application.Tests` | xUnit coverage for ingest, search, RAG, and chat |
| Frontend | `frontend/my-ai-chat` | Angular 18 chat UI for `POST /api/chat` |

`DocumentChunk.Embedding` is a `float[]?` in Domain. Infrastructure maps it to PostgreSQL `vector(1536)` with `Pgvector.EntityFrameworkCore`.

Chat and RAG are **read-only** against documents. They do not change document or chunk data.

---

## Project Structure

```text
MyAIProject/
├── MyAi.slnx
├── src/
│   ├── MyAi.Api/                  # ASP.NET Core host, Swagger, documents, search, RAG, chat
│   ├── MyAi.Application/          # CQRS commands/queries, validators, RAG service
│   ├── MyAi.Domain/               # Entities and domain rules
│   └── MyAi.Infrastructure/       # EF Core, OpenAI, pgvector, PdfPig
├── tests/
│   └── MyAi.Application.Tests/    # Backend automated tests
└── frontend/
    └── my-ai-chat/                # Angular 18 chat UI
```

Solution file: `MyAi.slnx`.

| Path | Responsibility |
| --- | --- |
| `src/MyAi.Api` | HTTP host, controllers, Swagger, Development CORS for the Angular app |
| `src/MyAi.Application` | CQRS/MediatR use cases, FluentValidation, RAG orchestration |
| `src/MyAi.Domain` | Document and chunk entities with no infrastructure types |
| `src/MyAi.Infrastructure` | Database, OpenAI clients, file storage, text extraction |
| `tests/` | xUnit tests for Application behavior |
| `frontend/my-ai-chat` | Standalone Angular chat component and `ChatService` |

---

## RAG Pipeline

```text
Document Upload
→ Text Extraction
→ Document Chunking
→ OpenAI Embeddings
→ PostgreSQL + pgvector
→ Semantic Search
→ Context Construction
→ Grounded Prompt
→ OpenAI LLM
→ Answer + Sources
```

Runtime chat flow:

```text
User
    ↓
Angular Chat UI
    ↓
POST /api/chat
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
    ↓
Angular UI
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
| 8. Angular UI | The chat screen calls `POST /api/chat` and renders the answer plus sources | Session-only (not persisted) |

Re-running embedding generation **updates** the existing chunk rows. It does not insert duplicate chunks or duplicate embedding records.

---

## Angular Chat UI (Step 11)

The frontend is an **Angular 18** standalone application at `frontend/my-ai-chat`.

It provides a chat interface that calls **`POST /api/chat`** through `ChatService`. The API base URL comes from `src/environments/environment.ts` (`apiBaseUrl`), not from hardcoded component URLs.

The UI displays:

- User messages
- Assistant answers
- Source documents (`fileName`)
- Chunk information (`documentId`, `chunkIndex`)
- Similarity scores (shown as a percentage)

It also includes:

- A loading state while the request is in progress
- User-friendly error handling (validation messages and connection failures)
- Disabled Send while a request is running
- Empty / whitespace question validation
- A responsive layout

The current page session keeps turns in memory only. Closing or refreshing the browser clears the transcript.

Not implemented in the UI: authentication, persisted chat history, streaming, SignalR, AI agents, tool calling, file upload, or OCR.

Local Angular development talks to `http://localhost:5235`. In Development the API allows CORS from `http://localhost:4200`. Use the API **http** profile (port **5235**) with the Angular app. Swagger on `https://localhost:7003` is HTTPS; `http://localhost:7003` is not a valid API URL.

---

## API Endpoints

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

## Chat API Example

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

### Response

The response contains:

- `answer` — grounded model output
- `sources` — retrieved chunks used for the answer
- `documentId`
- `chunkId`
- `chunkIndex`
- `fileName`
- `similarity` — cosine similarity (`1 - pgvector cosine distance`)

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

---

## Semantic Search Example

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
  "query": "How can a system find information based on meaning instead of matching exact words?",
  "topK": 5
}
```

PostgreSQL + pgvector performs vector similarity search and returns the most relevant chunks.

---

## Grounding Behavior

The application is designed to answer from retrieved document context.

System instructions tell the model:

- Use only the provided document excerpts as the factual source
- Do not invent facts that are not present in the context
- If the context does not contain enough information, state that the answer cannot be determined from the provided documents

When no relevant chunks are retrieved, the API returns:

```text
No relevant information was found in the uploaded documents.
```

When chunks are retrieved but they do not contain the answer, the model is instructed to respond that:

```text
The answer cannot be determined from the provided documents.
```

Retrieved document text is treated as **untrusted reference material**, not as system instructions.

This is prompt-and-retrieval grounding, not an absolute mathematical guarantee against hallucinations.

---

## Database

Main tables / entities:

| Table | Entity | Purpose |
| --- | --- | --- |
| `Documents` | `Document` | Uploaded file metadata, status, and extracted text |
| `DocumentChunks` | `DocumentChunk` | Chunk text plus the embedding vector |
| `__EFMigrationsHistory` | EF Core | Applied migrations |

`DocumentChunks.Embedding` is stored as PostgreSQL **pgvector** type **`vector(1536)`**, matching OpenAI `text-embedding-3-small`.

- Database: `MyAiDb` (local PostgreSQL 16)
- Extension: `vector`

EF Core owns the schema. The `AddDocumentChunkEmbedding` migration enables the `vector` extension and converts the embedding column to `vector(1536)`.

The PostgreSQL server must have the **pgvector** extension files installed before `CREATE EXTENSION vector` can succeed.

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

## Configuration

Secrets are **not** committed. `appsettings.json` may contain non-secret defaults. The OpenAI API key and the PostgreSQL password belong in **.NET User Secrets** or environment variables.

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

Angular API base URL (`frontend/my-ai-chat/src/environments/environment.ts`):

```ts
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5235'
};
```

---

## Running the Backend

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

Default launch profile (`http`): `http://localhost:5235`

HTTPS profile also binds `https://localhost:7003`. For the Angular UI, use **`http://localhost:5235`**.

---

## Running the Angular Frontend

```powershell
cd frontend/my-ai-chat
npm install
npm start
```

`npm start` runs `ng serve`. The app is served at **`http://localhost:4200`**.

Start the API on `http://localhost:5235` first, then ask a question in the chat UI.

```powershell
cd frontend/my-ai-chat
npx ng build
```

---

## Testing

Verified in this workspace during Step 11:

| Check | Result |
| --- | --- |
| `dotnet test tests/MyAi.Application.Tests/MyAi.Application.Tests.csproj` | **69 passed**, 0 failed |
| `npx ng test --watch=false --browsers=ChromeHeadless` (in `frontend/my-ai-chat`) | **7 passed** |
| `npx ng build` | succeeded |

```powershell
dotnet build MyAi.slnx
dotnet test tests/MyAi.Application.Tests/MyAi.Application.Tests.csproj
```

```powershell
cd frontend/my-ai-chat
npx ng test --watch=false --browsers=ChromeHeadless
npx ng build
```

---

## Technology Stack

**Backend**

- ASP.NET Core
- C#
- Entity Framework Core
- CQRS
- MediatR
- FluentValidation
- Clean Architecture

**AI**

- OpenAI
- Embeddings (`text-embedding-3-small`, 1536 dimensions)
- LLM (`gpt-4o-mini` by default)
- RAG
- Semantic Search

**Database**

- PostgreSQL
- pgvector

**Frontend**

- Angular 18
- TypeScript
- HTML/CSS

**Document Processing**

- PDF (PdfPig)
- TXT (UTF-8)

---

## Current Project Status

| Step | Feature | Status |
| --- | --- | --- |
| 1 | Clean Architecture Foundation | Complete |
| 2 | Document Upload | Complete |
| 3 | Text Extraction | Complete |
| 4 | Document Chunking | Complete |
| 5 | Embeddings | Complete |
| 6 | PostgreSQL + pgvector | Complete |
| 7 | Semantic Search | Complete |
| 8 | OpenAI LLM Integration | Complete |
| 9 | RAG Pipeline | Complete |
| 10 | Chat API | Complete |
| 11 | Angular Chat UI | Complete |

---

## Future Improvements

These items are **not implemented**.

- Document structure / fidelity validation
- Topic / heading-aware chunking
- Table-aware extraction
- Retrieval similarity threshold
- Reranking
- Stronger answer / source validation
- OCR for scanned PDFs
- Authentication
- Conversation history
- Streaming responses
- AI agents / tool calling
- Automatic ingest in a single call (upload → extract → chunk → embed)
- Vision / image processing
- External integrations (email, calendar, billing)

---

## GitHub Project

[https://github.com/atifch319/ai-rag-dotnet-angular](https://github.com/atifch319/ai-rag-dotnet-angular)

---

## Author

**Muhammad Atif**

Senior .NET Developer | ASP.NET Core | C# | Angular | AI
