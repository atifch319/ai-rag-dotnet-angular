import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ChatService } from '../services/chat.service';
import { ChatComponent } from './chat.component';

describe('ChatComponent', () => {
  let fixture: ComponentFixture<ChatComponent>;
  let component: ChatComponent;
  let chatService: jasmine.SpyObj<ChatService>;

  beforeEach(async () => {
    chatService = jasmine.createSpyObj('ChatService', ['send']);

    await TestBed.configureTestingModule({
      imports: [ChatComponent],
      providers: [{ provide: ChatService, useValue: chatService }]
    }).compileComponents();

    fixture = TestBed.createComponent(ChatComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('does not send an empty question', () => {
    component.message = '   ';
    component.send();
    expect(chatService.send).not.toHaveBeenCalled();
  });

  it('disables send while a request is running', () => {
    component.message = 'What is semantic search?';
    expect(component.canSend).toBeTrue();
    component.loading = true;
    expect(component.canSend).toBeFalse();
  });

  it('renders the assistant answer, citations, and evidence', () => {
    chatService.send.and.returnValue(of({
      message: 'What is semantic search?',
      answer: 'Semantic search focuses on meaning.',
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
    }));

    component.message = 'What is semantic search?';
    component.send();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('What is semantic search?');
    expect(text).toContain('Semantic search focuses on meaning.');
    expect(text).toContain('[1]');
    expect(text).toContain('RAG_Test_Document_25_Pages.txt');
    expect(text).toContain('Chunk 9');
    expect(text).toContain('40.8%');
    expect(text).toContain('Evidence');
    expect(text).toContain('exact keyword matching');
    expect(component.loading).toBeFalse();
  });

  it('renders multiple sources in backend order', () => {
    chatService.send.and.returnValue(of({
      message: 'What is semantic search?',
      answer: 'It ranks text by meaning.',
      sources: [
        {
          documentId: 5,
          chunkId: 39,
          chunkIndex: 9,
          fileName: 'RAG_Test_Document_25_Pages.txt',
          similarity: 0.4077,
          content: 'First retrieved chunk.'
        },
        {
          documentId: 8,
          chunkId: 12,
          chunkIndex: 1,
          fileName: 'RAG_Test_Document_Embedding.pdf',
          similarity: 0.393,
          content: 'Second retrieved chunk.'
        }
      ]
    }));

    component.message = 'What is semantic search?';
    component.send();
    fixture.detectChanges();

    const items = fixture.nativeElement.querySelectorAll('.sources li') as NodeListOf<HTMLElement>;
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('[1]');
    expect(items[0].textContent).toContain('RAG_Test_Document_25_Pages.txt');
    expect(items[0].textContent).toContain('Chunk 9');
    expect(items[0].textContent).toContain('40.8%');
    expect(items[1].textContent).toContain('[2]');
    expect(items[1].textContent).toContain('RAG_Test_Document_Embedding.pdf');
    expect(items[1].textContent).toContain('39.3%');
  });

  it('hides the sources section when the API returns no sources', () => {
    chatService.send.and.returnValue(of({
      message: "What is the CEO's favorite food?",
      answer: 'The answer cannot be determined from the provided documents.',
      sources: []
    }));

    component.message = "What is the CEO's favorite food?";
    component.send();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('The answer cannot be determined from the provided documents.');
    expect(fixture.nativeElement.querySelector('.sources')).toBeNull();
  });

  it('shows an API validation error', () => {
    chatService.send.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 400,
      error: { title: 'Validation failed', errors: { Message: ['Message must not be empty.'] } }
    })));

    component.message = 'What is semantic search?';
    component.send();
    fixture.detectChanges();

    expect(component.error).toContain('Message must not be empty.');
    expect(component.loading).toBeFalse();
  });
});
