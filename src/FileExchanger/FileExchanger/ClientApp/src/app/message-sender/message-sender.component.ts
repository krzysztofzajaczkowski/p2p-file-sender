import {Component, OnDestroy, OnInit} from '@angular/core';
import {interval, Subscription} from 'rxjs';
import {take} from 'rxjs/operators';

@Component({
  selector: 'app-message-sender',
  templateUrl: './message-sender.component.html'
})
export class MessageSenderComponent implements OnInit, OnDestroy {
  private encodings: string[] = ['ECB', 'CBC', 'CFB', 'OFB'];
  private obs: Subscription;
  message: string;
  progress = 0;
  chosenEncoding: string;

  constructor() {
  }

  ngOnInit(): void {
  }

  ngOnDestroy(): void {
    if (this.obs) {
      this.obs.unsubscribe();
    }
  }

  getEncodings(): string[] {
    return this.encodings;
  }

  sendMessage() {
    this.obs = interval(200).pipe(take(101)).subscribe({
      next: progress => this.progress = progress
    });
  }

  get senderDisabled(): boolean {
    return !this.message || this.message === '' || !this.chosenEncoding;
  }
}
