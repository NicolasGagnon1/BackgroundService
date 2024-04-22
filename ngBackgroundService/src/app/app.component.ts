import { AccountService } from './services/account.service';
import { Component } from '@angular/core';

// On doit commencer par ajouter signalr dans les node_modules: npm install @microsoft/signalr
// Ensuite on inclut la librairie
import * as signalR from "@microsoft/signalr"

interface RoundResult{
  winners:string[],
  nbClicks:number
}

interface UserData{
  score:number,
  multiplier:number
}

interface GameInfo{
  multiplierCost:number,
  nbWins:number;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent {
  title = 'ngBackgroundService';

  baseUrl = "https://localhost:7056/";

  // Ajouter une variable nbWins
  nbWins = 0;

  private hubConnection?: signalR.HubConnection

  isConnected = false;
  nbClicks = 0;
  // TODO: Ajouter 3 variables: Le multiplier, le multiplierCost, mais également le multiplierIntialCost pour remettre à jour multiplierCost après chaque fin de round (ou sinon on peut passer l'information dans l'appel qui vient du Hub!)
  multiplier= 1;
  multiplierCost = 0;
  multplierInitialCost = 10;
  constructor(public account:AccountService){
  }

  Increment() {
    //TODO: Augmenter le nbClicks par la valeur du multiplicateur
    this.nbClicks += this.multiplier;
    this.hubConnection!.invoke('Increment')
  }

  BuyMultiplier() {
    // TODO: Implémenter la méthode qui permet d'acheter un niveau de multiplier (Appel au Hub!)
    if (this.multiplierCost <= this.nbClicks) {
      this.multiplier *= 2;
      this.hubConnection!.invoke('BuyMultiplier')
    }
  }

  async register(){
    try{
      await this.account.register();
    }
    catch(e){
      alert("Erreur pendant l'enregistrement!!!!!");
      return;
    }
    alert("L'enregistrement a été un succès!");
  }

  async login(){
    await this.account.login();
  }

  async logout(){
    await this.account.logout();

    if(this.hubConnection?.state == signalR.HubConnectionState.Connected)
      this.hubConnection.stop();
    this.isConnected = false;
  }

  isLoggedIn() : Boolean{
    return this.account.isLoggedIn();
  }

  connectToHub() {
    this.hubConnection = new signalR.HubConnectionBuilder()
                              .withUrl(this.baseUrl + 'game')
                              .build();

    this.hubConnection
      .start()
      .then(() => {

        this.hubConnection!.on('GameInfo', (data:GameInfo) => {
          this.isConnected = true;

          // TODO: Mettre à jour les variables pour le coût du multiplier et le nbWins
        });

        this.hubConnection!.on('NbWins', (data) => {
          console.log(data);
          this.nbWins = data;
        });

        this.hubConnection!.on('UserData', (data:UserData) => {
          console.log(data);
          this.multiplier = data.multiplier;
          this.nbClicks = data.score;

        });

        this.hubConnection!.on('EndRound', (data:RoundResult) => {
          this.nbClicks = 0;
          // TODO: Reset du multiplierCost et le multiplier
          this.multiplier = 1;
          this.multiplierCost = 0;
          // TODO: Si le joueur a gagné, on augmente nbWins

          if(data.nbClicks > 0){
            let phrase = " a gagné avec ";
            if(data.winners.length > 1)
              phrase = " ont gagnées avec "
            alert(data.winners.join(", ") + phrase + data.nbClicks + " clicks!");
          }
          else{
            alert("Aucun gagnant...");
          }

        });
      })
      .catch(err => console.log('Error while starting connection: ' + err))
  }
}
