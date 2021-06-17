import {Component, OnDestroy, OnInit} from '@angular/core';
import {interval, Subscription} from 'rxjs';
import {take} from 'rxjs/operators';

@Component({
  selector: 'app-file-sender',
  templateUrl: './file-sender.component.html'
})
export class FileSenderComponent implements OnInit, OnDestroy {
  private encodings: string[] = ['ECB', 'CBC', 'CFB', 'OFB'];
  private obs: Subscription;
  private sending: boolean;
  file: File;
  progress: number;
  chosenEncoding: string;

  constructor() {
  }

  ngOnInit(): void {
    this.progress = 0;
    this.sending = false;
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
    this.sending = true;
    this.obs = interval(200).pipe(take(101)).subscribe({
      next: progress => this.progress = progress,
      complete: () => this.sending = false
    });
  }

  get senderDisabled(): boolean {
    return !this.file || !this.chosenEncoding || this.sending;
  }
}
