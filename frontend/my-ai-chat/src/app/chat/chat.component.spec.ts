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

  it('renders the assistant answer and sources', () => {
    chatService.send.and.returnValue(of({
      message: 'What is semantic search?',
      answer: 'Semantic search focuses on meaning.',
      sources: [
        {
          documentId: 5,
          chunkId: 39,
          chunkIndex: 9,
          fileName: 'RAG_Test_Document_25_Pages.txt',
          similarity: 0.4076
        }
      ]
    }));

    component.message = 'What is semantic search?';
    component.send();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('What is semantic search?');
    expect(text).toContain('Semantic search focuses on meaning.');
    expect(text).toContain('RAG_Test_Document_25_Pages.txt');
    expect(text).toContain('40.8%');
    expect(component.loading).toBeFalse();
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
