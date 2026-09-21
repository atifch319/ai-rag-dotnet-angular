export interface ChatRequest {
  message: string;
  topK: number;
}

export interface ChatSource {
  documentId: number;
  chunkId: number;
  chunkIndex: number;
  fileName: string;
  similarity: number;
}

export interface ChatResponse {
  message: string;
  answer: string;
  sources: ChatSource[];
}

export interface ChatApiError {
  title?: string;
  errors?: Record<string, string[]>;
}

export interface ChatTurn {
  role: 'user' | 'assistant';
  text: string;
  sources?: ChatSource[];
}
