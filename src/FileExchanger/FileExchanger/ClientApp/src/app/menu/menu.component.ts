import {Component, OnDestroy, OnInit} from '@angular/core';
import {Router} from '@angular/router';
import {MatSnackBar} from '@angular/material/snack-bar';

@Component({
  selector: 'app-home',
  templateUrl: './menu.component.html'
})
export class MenuComponent implements OnInit, OnDestroy {
  sessionKeyGenerated: boolean;

  constructor(private router: Router, private snackBar: MatSnackBar) {
  }

  generateSessionKey() {
    console.log(Date.now());
    this.snackBar.open('Session key generated!', null, {
      duration: 2000
    });
    this.sessionKeyGenerated = true;
  }

  ngOnInit(): void {
    this.sessionKeyGenerated = false;
  }

  ngOnDestroy(): void {
  }
}
