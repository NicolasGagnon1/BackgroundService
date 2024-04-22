using BackgroundService.Data;
using BackgroundService.Hubs;
using BackgroundService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SuperChance.DTOs;
using System.Numerics;

namespace BackgroundService.Services
{
    public class UserData
    {
        public int Score { get; set; } = 0;
        public int Multiplier { get; set; } = 1;
        // TODO: Ajouter une propriété pour le multiplier
    }

    public class Game : Microsoft.Extensions.Hosting.BackgroundService
    {
        public const int DELAY = 30 * 1000;
        public const int MULTIPLIER_BASE_PRICE = 10;

        private Dictionary<string, UserData> _data = new();

        private IHubContext<GameHub> _gameHub;

        private IServiceScopeFactory _serviceScopeFactory;

        public Game(IHubContext<GameHub> gameHub, IServiceScopeFactory serviceScopeFactory)
        {
            _gameHub = gameHub;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public void AddUser(string userId)
        {
            _data[userId] = new UserData();
        }

        public void RemoveUser(string userId)
        {
            _data.Remove(userId);
        }

        public void Increment(string userId)
        {
            UserData userData = _data[userId];
            // TODO: Ajouter la valeur du muliplier au lieu d'ajouter 1
            userData.Score += userData.Multiplier;
        }

        // TODO: Ajouter une méthode pour acheter un multiplier. Le coût est le prix de base * le multiplier actuel
        // Les prix sont donc de 10, 20, 40, 80, 160 (Si le prix de base est 10)
        // Réduire le score du coût du multiplier
        // Doubler le multiplier du joueur

        public async Task BuyMultiplier(string userId)
        {
            UserData userData = _data[userId];
            int cout = MULTIPLIER_BASE_PRICE * userData.Multiplier;
            if(userData.Score > cout)
            {
                userData.Score -= cout;
                userData.Multiplier *= 2;
                _gameHub.Clients.Users(userId).SendAsync("UserData", userData);
            }
        }

        public async Task EndRound(CancellationToken stoppingToken)
        {
            List<string> winners = new List<string>();
            int biggestValue = 0;
            // Reset des compteurs
            foreach (var key in _data.Keys)
            {
                int value = _data[key].Score;
                if (value > 0 && value >= biggestValue)
                {
                    if (value > biggestValue)
                    {
                        winners.Clear();
                        biggestValue = value;
                    }
                    winners.Add(key);
                }
            }

            // Reset
            foreach (var key in _data.Keys)
            {
                // TODO: On remet le multiplier à 1!
                _data[key].Multiplier = 1;
                _data[key].Score = 0;
            }

            // Aucune participation!
            if (biggestValue == 0)
            {
                RoundResult noResult = new RoundResult()
                {
                    Winners = null,
                    NbClicks = 0
                };
                await _gameHub.Clients.All.SendAsync("EndRound", noResult, stoppingToken);
                return;
            }

            using (IServiceScope scope = _serviceScopeFactory.CreateScope())
            {
                BackgroundServiceContext backgroundServiceContext =
                    scope.ServiceProvider.GetRequiredService<BackgroundServiceContext>();

                // TODO: Mettre à jour et sauvegarder le nbWins des joueurs
                foreach (string userId in winners)
                {
                    Player player = backgroundServiceContext.Player.Where(x => x.UserId == userId).SingleOrDefault();
                    player.NbWins++;
                    _gameHub.Clients.Users(winners).SendAsync("NbWins", player.NbWins, stoppingToken);
                    backgroundServiceContext.SaveChanges();
                }
                //_gameHub.Clients.All.SendAsync("NbWins", backgroundServiceContext., stoppingToken);

                List<IdentityUser> users = await backgroundServiceContext.Users.Where(u => winners.Contains(u.Id)).ToListAsync();

                RoundResult roundResult = new RoundResult()
                {
                    Winners = users.Select(p => p.UserName)!,
                    NbClicks = biggestValue
                };
                await _gameHub.Clients.All.SendAsync("EndRound", roundResult, stoppingToken);
            }
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(DELAY, stoppingToken);
                await EndRound(stoppingToken);
            }
        }
    }
}
