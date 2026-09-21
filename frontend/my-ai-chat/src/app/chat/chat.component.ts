import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatApiError, ChatTurn } from '../models/chat';
import { ChatService } from '../services/chat.service';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.css'
})
export class ChatComponent {
  message = '';
  topK = 5;
  loading = false;
  error = '';
  turns: ChatTurn[] = [];

  constructor(private readonly chatService: ChatService) {}

  get canSend(): boolean {
    return !this.loading && this.message.trim().length > 0;
  }

  send(): void {
    debugger
    const text = this.message.trim();
    if (!text || this.loading) {
      return;
    }

    this.error = '';
    this.loading = true;
    this.turns.push({ role: 'user', text });
    this.message = '';

    this.chatService.send({ message: text, topK: this.topK }).subscribe({
      next: (response) => {
        debugger
        this.turns.push({
          role: 'assistant',
          text: response.answer,
          sources: response.sources ?? []
        });
        this.loading = false;
      },
      error: (err: HttpErrorResponse) => {
        debugger
        this.error = this.readError(err);
        this.loading = false;
      }
    });
  }

  similarityPercent(similarity: number): string {
    return `${(similarity * 100).toFixed(1)}%`;
  }

  private readError(err: HttpErrorResponse): string {
    const body = err.error as ChatApiError | undefined;
    const fieldErrors = body?.errors
      ? Object.values(body.errors).flat().filter(Boolean)
      : [];

    if (fieldErrors.length > 0) {
      return fieldErrors.join(' ');
    }

    if (body?.title) {
      return body.title;
    }

    if (err.status === 0) {
      return 'Could not reach the API. Confirm the backend is running.';
    }

    return 'The request failed. Please try again.';
  }
}
