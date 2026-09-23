import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { ChatResponse } from '../models/chat';
import { ChatService } from './chat.service';

describe('ChatService', () => {
  let service: ChatService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ChatService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the chat request to the configured API URL', () => {
    const response: ChatResponse = {
      message: 'What is semantic search?',
      answer: 'Semantic search ranks text by meaning.',
      sources: [
        {
          documentId: 5,
          chunkId: 39,
          chunkIndex: 9,
          fileName: 'RAG_Test_Document_25_Pages.txt',
          similarity: 0.4076,
          content: 'Semantic search focuses on meaning rather than exact keyword matching.'
        }
      ]
    };

    service.send({ message: 'What is semantic search?', topK: 5 }).subscribe((body) => {
      expect(body.answer).toBe(response.answer);
      expect(body.sources.length).toBe(1);
      expect(body.sources[0].chunkId).toBe(39);
      expect(body.sources[0].similarity).toBe(0.4076);
      expect(body.sources[0].content).toContain('exact keyword matching');
      expect(body.sources[0]).not.toEqual(jasmine.objectContaining({ embedding: jasmine.anything() }));
    });

    const req = http.expectOne(`${environment.apiBaseUrl}/api/chat`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ message: 'What is semantic search?', topK: 5 });
    req.flush(response);
  });
});
