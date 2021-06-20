import {Injectable} from '@angular/core';
import {of, Subject} from 'rxjs';
import {delay} from 'rxjs/operators';

@Injectable()
export class SubscriptionService {
  receiver$: any;
  publisher$ = new Subject();

  constructor() {
    console.log('sending...');
    this.receiver$ = of('Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.')
      .pipe(delay(2000))
      .subscribe( mess => {
        this.publisher$.next(mess);
    });
  }

}
