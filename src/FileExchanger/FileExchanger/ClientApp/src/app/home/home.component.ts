import {Component} from '@angular/core';
import {Router} from '@angular/router';
import {MatSnackBar} from '@angular/material/snack-bar';
import {MatDialog, MatDialogRef} from '@angular/material';
import {ChooserComponent} from '../chooser/chooser.component';
import {filter} from 'rxjs/operators';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html'
})
export class HomeComponent {
  constructor(private router: Router, private snackBar: MatSnackBar,
              private dialog: MatDialog) {
  }
  private dialogRef: MatDialogRef<ChooserComponent>;
  password: string;

  login() {
    if (this.password) {
      this.dialogRef = this.dialog.open(ChooserComponent);
      this.dialogRef.afterClosed()
        .pipe(filter(result => result !== undefined))
        .subscribe(result => {
          console.log(`Got address: ${result}`);
        });
    } else {
      this.snackBar.open('Log in failed...', 'Close', {
        duration: 2000
      });
    }
  }

}
