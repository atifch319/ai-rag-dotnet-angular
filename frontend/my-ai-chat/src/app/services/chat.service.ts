import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ChatRequest, ChatResponse } from '../models/chat';

@Injectable({
  providedIn: 'root'
})
export class ChatService {
  private readonly url = `${environment.apiBaseUrl}/api/chat`;

  constructor(private readonly http: HttpClient) {}

  send(request: ChatRequest): Observable<ChatResponse> {
    return this.http.post<ChatResponse>(this.url, request);
  }
}
