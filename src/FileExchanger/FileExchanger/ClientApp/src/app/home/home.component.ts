import {Component} from '@angular/core';
import {Router} from '@angular/router';
import {MatSnackBar} from '@angular/material/snack-bar';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html'
})
export class HomeComponent {
  constructor(private router: Router, private _snackBar: MatSnackBar) {
  }

  password: string;

  login() {
    if (this.password) {
      this.router.navigate(['/menu'])
        .then(() => console.log('Navigated to counter'));
    } else {
      this._snackBar.open('Log in failed...', 'Close', {
        duration: 2000
      });
    }
  }

}
