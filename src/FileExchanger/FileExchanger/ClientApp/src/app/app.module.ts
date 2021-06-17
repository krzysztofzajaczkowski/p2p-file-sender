import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { RouterModule } from '@angular/router';

import { AppComponent } from './app.component';
import { HomeComponent } from './home/home.component';
import {
  MatButtonModule,
  MatDialogModule,
  MatFormFieldModule,
  MatInputModule,
  MatProgressBarModule, MatRadioModule,
  MatSnackBarModule
} from '@angular/material';
import {BrowserAnimationsModule} from '@angular/platform-browser/animations';
import {FileSenderComponent} from './file-sender/file-sender.component';
import {MessageSenderComponent} from './message-sender/message-sender.component';
import {MenuComponent} from './menu/menu.component';
import {ReceiverComponent} from './receiver/receiver.component';
import {SubscriptionService} from './services/subscription.service';
import {ChooserComponent} from './chooser/chooser.component';

@NgModule({
  declarations: [
    AppComponent,
    HomeComponent,
    FileSenderComponent,
    MessageSenderComponent,
    MenuComponent,
    ReceiverComponent,
    ChooserComponent
  ],
  imports: [
    BrowserModule.withServerTransition({appId: 'ng-cli-universal'}),
    HttpClientModule,
    FormsModule,
    MatSnackBarModule,
    MatDialogModule,
    BrowserAnimationsModule,
    RouterModule.forRoot([
      {path: '', component: HomeComponent, pathMatch: 'full'},
      {path: 'file-sender', component: FileSenderComponent},
      {path: 'message-sender', component: MessageSenderComponent},
      {path: 'menu', component: MenuComponent}
    ]),
    MatProgressBarModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule
  ],
  providers: [SubscriptionService],
  bootstrap: [AppComponent],
  entryComponents: [
    ReceiverComponent,
    ChooserComponent
  ]
})
export class AppModule { }
