import {Component, Inject, OnDestroy, OnInit} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material';
import {Router} from '@angular/router';

@Component({
  selector: 'app-receiver',
  templateUrl: './chooser.component.html',
  styleUrls: ['./chooser.component.css']
})
export class ChooserComponent implements OnInit, OnDestroy {
  address: string;

  constructor(@Inject(MAT_DIALOG_DATA) public data: any,
              private dialogRef: MatDialogRef<ChooserComponent>,
              private router: Router) {
  }

  ngOnInit(): void {
  }

  ngOnDestroy(): void {
  }

  clickedOk() {
    this.dialogRef.close(this.address);
    this.router.navigate(['/menu']).then(() => console.log('User logged in!'));
  }
}
