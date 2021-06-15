import {Component, OnDestroy, OnInit} from '@angular/core';
import {Router} from '@angular/router';
import {interval, Subscription} from 'rxjs';
import {map} from 'rxjs/operators';

@Component({
  selector: 'app-file-sender',
  templateUrl: './file-sender.component.html'
})
export class FileSenderComponent implements OnInit, OnDestroy {
  private encodings: string[] = ['ECB', 'CBC', 'CFB', 'OFB'];
  public progress = 0;
  private obs: Subscription;

  constructor(private router: Router ) {
  }

  ngOnInit(): void {
    this.obs = interval(250).pipe(map(x => x % 101)).subscribe(progress => this.progress = progress );
  }

  ngOnDestroy() {
    this.obs.unsubscribe();
  }

  getEncodings(): string[] {
    return this.encodings;
  }
}