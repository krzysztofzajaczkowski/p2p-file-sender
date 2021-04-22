import {Component} from '@angular/core';
import {Router} from '@angular/router';
import {MatSnackBar} from '@angular/material/snack-bar';

@Component({
  selector: 'app-home',
  templateUrl: './menu.component.html'
})
export class MenuComponent {
  constructor(private router: Router, private _snackBar: MatSnackBar ) {
  }


  generateSessionKey() {
    this._snackBar.open('Session key generated!', null, {
      duration: 2000
    });
  }
}
