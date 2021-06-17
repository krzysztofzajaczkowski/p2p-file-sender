import {Component, Inject, OnDestroy, OnInit} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material';

@Component({
  selector: 'app-receiver',
  templateUrl: './receiver.component.html',
  styleUrls: ['./receiver.component.css']
})
export class ReceiverComponent implements OnInit, OnDestroy {
  filename: string;
  receivingFile: boolean;
  message: string;
  replyWith: string;

  constructor(@Inject(MAT_DIALOG_DATA) public data: any,
              private dialogRef: MatDialogRef<ReceiverComponent>) {
    this.receivingFile = data.receivingFile;
    if (this.receivingFile) {
      this.filename = data.filename;
    } else {
      this.message = data.message;
    }
  }

  ngOnInit(): void {
    this.replyWith = this.receivingFile ? '/file-sender' : '/message-sender';
  }

  ngOnDestroy() : void {
  }

  reply() {
    this.dialogRef.close();
  }
}
