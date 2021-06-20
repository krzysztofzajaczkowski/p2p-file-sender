import {Component, OnDestroy, OnInit} from '@angular/core';
import {SubscriptionService} from './services/subscription.service';
import {Subscription} from 'rxjs';
import {MatDialog} from '@angular/material';
import {ReceiverComponent} from './receiver/receiver.component';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html'
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'BSK Project';
  subscription: Subscription;
  constructor(private subscriptionService: SubscriptionService, private dialog: MatDialog) {
  }

  ngOnInit(): void {
    this.subscription = this.subscriptionService.publisher$.subscribe(mess => {
      this.dialog.open(ReceiverComponent, {
        data: {
          receivingFile: false,
          filename: mess,
          message: mess
        }
      });
    });
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }
}
