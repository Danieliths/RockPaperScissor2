﻿

namespace RockPaperScissor
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing;
    using Microsoft.Extensions.Configuration;
    using ChangeFeedProcessorLibrary = Microsoft.Azure.Documents.ChangeFeedProcessor;
    using System.Linq;

    public class Program
    {
        private static readonly string monitoredContainer = "Games";
        private static readonly string leasesContainer = "leases";
        private static readonly string partitionKeyPath = "/id";
        private static Game currentGame;
        private static Container container;
        private static Player player;
        private static ChangeFeedProcessor changeFeedProcessor;
        static async Task Main(string[] args)
        {
            string endpoint = "https://localhost:8081";
            string authKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            CosmosClient client = new CosmosClient(endpoint, authKey);
            CreatePlayer();
            await RunBasicChangeFeed("Games", client);
            while (player.IsDonePlaying == false)
            {
                await RunGame();
                await Task.Delay(3000);
            }
            await changeFeedProcessor.StopAsync();
            Console.WriteLine("Press any key to close...");
            Console.ReadKey(true);
        }
        private static async Task RunBasicChangeFeed(string databaseId, CosmosClient client)
        {
            await Program.InitializeAsync(databaseId, client);
            Container leaseContainer = client.GetContainer(databaseId, Program.leasesContainer);
            container = client.GetContainer(databaseId, Program.monitoredContainer);
            changeFeedProcessor = container
                .GetChangeFeedProcessorBuilder<Game>("RockPaperScissor", HandleChangesAsync)
                .WithInstanceName("consoleHost")
                .WithLeaseContainer(leaseContainer)
                .Build();
            await changeFeedProcessor.StartAsync();
        }
        private static async Task HandleChangesAsync(IReadOnlyCollection<Game> changes, CancellationToken cancellationToken)
        {
            // Rätt Gamecheck
            if (currentGame.Id != changes.First().Id)
            {
                return;
            }

            // Uppdatera currentGame med ändringar från db
            currentGame = changes.First();

            // Säkerställer att spelaren är i ett game
            if (player.HasJoinedGame == false)
            {
                return;
            }

            // Skriv ut i väntan på att motstondare hittas
            if (currentGame.IsJoinable == true && player.HasJoinedGame)
            {
                Console.WriteLine("Waiting for player to join");
                await Task.Delay(50);
                return;
            }
            
            // Skriv ut vinnare
            if (currentGame.ToMove == Game.PlayerType.None)
            {
                Console.Clear();
                PrintWinner();
                return;
            }

            // Ser vem som vann senaste rundan och skriver ut det
            if (currentGame.CreatorMove.Count() == currentGame.JoinerMove.Count() && currentGame.CreatorMove.Count() == currentGame.Turn)
            {
                Console.Clear();
                switch (currentGame.CreatorMove.Last())
                {
                    case Game.Move.Rock:
                        if (currentGame.JoinerMove.Last() == Game.Move.Rock)
                        {
                            Draw();
                        }
                        if (currentGame.JoinerMove.Last() == Game.Move.Paper)
                        {
                            JoinerWin();
                        }
                        if (currentGame.JoinerMove.Last() == Game.Move.Scissors)
                        {
                            CreatorWin();
                        }
                        break;
                    case Game.Move.Paper:
                        if (currentGame.JoinerMove.Last() == Game.Move.Rock)
                        {
                            CreatorWin();
                        }
                        if (currentGame.JoinerMove.Last() == Game.Move.Paper)
                        {
                            Draw();
                        }
                        if (currentGame.JoinerMove.Last() == Game.Move.Scissors)
                        {
                            JoinerWin();
                        }
                        break;
                    case Game.Move.Scissors:
                        if (currentGame.JoinerMove.Last() == Game.Move.Rock)
                        {
                            JoinerWin();
                        }
                        if (currentGame.JoinerMove.Last() == Game.Move.Paper)
                        {
                            CreatorWin();
                        }
                        if (currentGame.JoinerMove.Last() == Game.Move.Scissors)
                        {
                            Draw();
                        }
                        break;
                    default:
                        break;
                }
            }

            // Vinnare funnen check, sätter ToMove.None
            if (currentGame.FirstToNumberOfWins == currentGame.CreatorScore || currentGame.FirstToNumberOfWins == currentGame.JoinerScore)
            {
                Console.Clear();
                currentGame.IsGameCompleted = true;
                currentGame.ToMove = Game.PlayerType.None;
                currentGame.IsGameCompleted = true;
                await container.UpsertItemAsync<Game>(currentGame);
                return;
            }

            // vänta på din tur
            if (currentGame.ToMove != player.TypeOfPlayer)
            {
                if (player.TypeOfPlayer == Game.PlayerType.Joiner)
                {
                    Console.WriteLine($"Waiting for {currentGame.CreatorName} to move");
                }
                if (player.TypeOfPlayer == Game.PlayerType.Creator)
                {
                    Console.WriteLine($"Waiting for {currentGame.JoinerName} to move");
                }
                await Task.Delay(70);
                return;
            }
            
            MakeAMove();
            await container.UpsertItemAsync<Game>(currentGame);
            return;
        }
        private static async Task InitializeAsync(string databaseId, CosmosClient client)
        {
            Database database;
            // Recreate database
            try
            {
                database = await client.GetDatabase(databaseId).ReadAsync();
                //await database.DeleteAsync();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }

            database = await client.CreateDatabaseIfNotExistsAsync(databaseId);

            await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Program.monitoredContainer, Program.partitionKeyPath));

            await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Program.leasesContainer, Program.partitionKeyPath));
        }
        private static async Task RunGame()
        {
            player.HasJoinedGame = false;
            player.TypeOfPlayer = Game.PlayerType.None;
            await Menu();

            while (currentGame.IsGameCompleted == false)
            {
                await Task.Delay(50);
            }
        }
        private static async Task JoinGame()
        {
            Console.WriteLine("Looking for open game");
            if (container.GetItemLinqQueryable<Game>(true).Where(o => o.IsJoinable == true).Count() > 0)
            {
                foreach (var item in container.GetItemLinqQueryable<Game>(true).Where(o => o.IsJoinable == true))
                {
                    currentGame = item;
                    Console.WriteLine($"Joining game created by {currentGame.CreatorName}");
                    player.HasJoinedGame = true;
                    player.TypeOfPlayer = Game.PlayerType.Joiner;
                    currentGame.JoinerName = player.Name;
                    currentGame.IsJoinable = false;
                    await container.UpsertItemAsync<Game>(currentGame);
                    return;
                }
                
                await Task.Delay(500);
                return;
            }
            Console.WriteLine("No open game found");
        }
        private static async Task CreateGame()
        {
            await Task.Delay(500);
            currentGame = new Game(player.Name);
            await container.CreateItemAsync<Game>(currentGame); //new PartitionKey()
            player.HasJoinedGame = true;
            player.TypeOfPlayer = Game.PlayerType.Creator;
            currentGame.IsGameCompleted = false;
            Console.WriteLine("game created");
            await container.UpsertItemAsync<Game>(currentGame);
        }
        private static async Task Menu()
        {
            bool InputDone = false;
            while (InputDone == false)
            {
                Console.WriteLine();
                Console.WriteLine("1: Create game");
                Console.WriteLine("2: Join game");
                Console.WriteLine("3: Exit");
                var keyInput = Console.ReadKey(true).Key;
                switch (keyInput)
                {
                    case ConsoleKey.D1:
                        await CreateGame();
                        InputDone = true;
                        break;
                    case ConsoleKey.D2:
                        await JoinGame();
                        InputDone = true;
                        break;
                    case ConsoleKey.D3:
                        player.IsDonePlaying = true;
                        InputDone = true;
                        break;

                    default:
                        Console.WriteLine("Not a valid choice, try again");
                        break;
                }
            }
        }
        private static void CreatorWin()
        {
            currentGame.CreatorScore++;
            Console.WriteLine($"Turn: {currentGame.Turn} :  {currentGame.CreatorName} wins");
            Console.WriteLine($"{currentGame.CreatorName} has {currentGame.CreatorScore} wins");
            currentGame.Turn++;
        }
        private static void CreatePlayer()
        {
            Program.player = new Player("Temp", Game.PlayerType.None);
            Console.WriteLine("Enter name");
            player.Name = Console.ReadLine();
        }
        private static void JoinerWin()
        {
            currentGame.JoinerScore++;
            Console.WriteLine($"Turn: {currentGame.Turn} :  {currentGame.JoinerName} wins");
            Console.WriteLine($"{currentGame.JoinerName} has {currentGame.JoinerScore} wins");
            currentGame.Turn++;
        }
        private static void Draw()
        {
            Console.WriteLine($"Turn: {currentGame.Turn} ended in a Draw");
            currentGame.Turn++;
        }
        private static void MakeAMove()
        {
            Console.WriteLine();
            Console.WriteLine("\nYour turn to move");
            Console.WriteLine("Current score");
            Console.WriteLine($"First to {currentGame.FirstToNumberOfWins} wins");
            Console.WriteLine($"{currentGame.CreatorName}: {currentGame.CreatorScore} wins");
            Console.WriteLine($"{currentGame.JoinerName}: {currentGame.JoinerScore} wins");
            Console.WriteLine("1: Rock");
            Console.WriteLine("2: Paper");
            Console.WriteLine("3: Scissor");
            var keyInput = Console.ReadKey(true).Key;
            if (currentGame.ToMove == Game.PlayerType.Creator)
            {
                switch (keyInput)
                {
                    case ConsoleKey.D1:
                        currentGame.CreatorMove.Add(Game.Move.Rock);
                        break;
                    case ConsoleKey.D2:
                        currentGame.CreatorMove.Add(Game.Move.Paper);
                        break;
                    case ConsoleKey.D3:
                        currentGame.CreatorMove.Add(Game.Move.Scissors);
                        break;
                    default:
                        break;
                }
                currentGame.ToMove = Game.PlayerType.Joiner;
                return;
            }
            if (currentGame.ToMove == Game.PlayerType.Joiner)
            {
                switch (keyInput)
                {
                    case ConsoleKey.D1:
                        currentGame.JoinerMove.Add(Game.Move.Rock);
                        break;
                    case ConsoleKey.D2:
                        currentGame.JoinerMove.Add(Game.Move.Paper);
                        break;
                    case ConsoleKey.D3:
                        currentGame.JoinerMove.Add(Game.Move.Scissors);
                        break;
                    default:
                        break;
                }
                currentGame.ToMove = Game.PlayerType.Creator;
            }
        }
        private static void PrintWinner()
        {
            if (currentGame.CreatorScore > currentGame.JoinerScore)
            {
                Console.WriteLine($"congratz {currentGame.CreatorName} to winning over {currentGame.JoinerName}");
            }
            else
            {
                Console.WriteLine($"congratz {currentGame.JoinerName} to winning over {currentGame.CreatorName}");
            }
        }
    }
}
