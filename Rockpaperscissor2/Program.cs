

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
        private Game currentGame;
        private Container container;
        private Player player;
        static async Task Main(string[] args)
        {
            string endpoint = "https://localhost:8081";
            string authKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            
            CosmosClient client = new CosmosClient(endpoint, authKey);
            Program p = new Program();
            //startar up alla grejer de lyssnar efter
            Console.WriteLine($"\n1. Kör RunBasicChangeFeed.");
            await p.RunBasicChangeFeed("Games", client);
            

        }
        private async Task RunBasicChangeFeed(
            string databaseId,
            CosmosClient client)
        {
            await Program.InitializeAsync(databaseId, client);

            // <BasicInitialization>
            Container leaseContainer = client.GetContainer(databaseId, Program.leasesContainer);
            container = client.GetContainer(databaseId, Program.monitoredContainer);
            ChangeFeedProcessor changeFeedProcessor = container
                .GetChangeFeedProcessorBuilder<Game>("RockPaperScissor", this.HandleChangesAsync)
                    .WithInstanceName("consoleHost")
                    .WithLeaseContainer(leaseContainer)
                    .Build();
            // </BasicInitialization>

            Console.WriteLine("Booting...");
            await changeFeedProcessor.StartAsync();
            player = new Player("test",Game.PlayerType.Creator);
            await this.CreateGame(container,player);
            Console.WriteLine("game created");
            currentGame.IsGameCompleted = false; 
            await container.UpsertItemAsync<Game>(currentGame);
            while (currentGame.IsGameCompleted == false)
            {
            await Task.Delay(5000);

            }
            Console.WriteLine("Press any key to close...");
            Console.ReadKey();
            await changeFeedProcessor.StopAsync();
            
        }
        private async Task EnterPlayerName()
        {
            Console.WriteLine("Enter name");
            player.Name = Console.ReadLine();
            
            currentGame.CreatorName = player.Name;
            currentGame.JoinerName = "Pontus";
        }
        private async Task NameChange(string name)
        {

        }
        private void CreatorWin()
        {
            currentGame.CreatorScore++;
            Console.WriteLine($"Turn: {currentGame.Turn} :  {currentGame.CreatorName} wins");
            Console.WriteLine($"{currentGame.CreatorName} has {currentGame.CreatorScore} wins");
            currentGame.Turn++;
        }
        private void JoinerWin()
        {
            currentGame.JoinerScore++;
            Console.WriteLine($"Turn: {currentGame.Turn} :  {currentGame.JoinerName} wins");
            Console.WriteLine($"{currentGame.JoinerName} has {currentGame.JoinerScore} wins");
            currentGame.Turn++;
        }
        private void Draw()
        {
            Console.WriteLine($"Turn: {currentGame.Turn} ended in a Draw");
            currentGame.Turn++;
        }

        private async Task HandleChangesAsync(IReadOnlyCollection<Game> changes, CancellationToken cancellationToken)
        {
            currentGame = changes.First();
            //Rätt Gamecheck
            if (currentGame.Id != changes.First().Id)
                return;
            if (player.TypeOfPlayer == currentGame.ToMove || currentGame.ToMove == Game.PlayerType.None) // funkar ej. switcharunt
            {
                Console.WriteLine($"Waiting for your turn");
                return;
            }
            Console.Clear();
            //Skriv ut vinnare
            if (currentGame.ToMove == Game.PlayerType.None)
            {
                if (currentGame.CreatorScore > currentGame.JoinerScore)
                {
                    Console.WriteLine($"{currentGame.CreatorName} WINS congratz!");
                    
                }
                else
                {
                    Console.WriteLine($"{currentGame.JoinerName} WINS congratz!");
                }
                return;
            }
            //kollar om någon vann rundan skriver ut segraren av rundan
            if (currentGame.CreatorMove.Count() == currentGame.JoinerMove.Count() && currentGame.CreatorMove.Count() == currentGame.Turn)
            {
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
            //Vinnare funnen check, sätter ToMove.None
            if (currentGame.FirstToNumberOfWins == currentGame.CreatorScore || currentGame.FirstToNumberOfWins == currentGame.JoinerScore)
            {
                Console.WriteLine("inne i vinnare funnencheck");
                currentGame.IsGameCompleted = true;
                currentGame.ToMove = Game.PlayerType.None;
                await container.UpsertItemAsync<Game>(currentGame);
                return;
            }



            Console.WriteLine($"{currentGame.ToMove} turn to move");
            Console.WriteLine($"Current score");
            Console.WriteLine($"First to {currentGame.FirstToNumberOfWins} wins");
            Console.WriteLine($"{currentGame.CreatorName}: {currentGame.CreatorScore} wins");
            Console.WriteLine($"{currentGame.JoinerName}: {currentGame.JoinerScore} wins");
            Console.WriteLine("1: Rock");
            Console.WriteLine("2: Paper");
            Console.WriteLine("3: Scissor");
            var keyInput = Console.ReadKey().Key;
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
                await container.UpsertItemAsync<Game>(currentGame);
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


            
            await container.UpsertItemAsync<Game>(currentGame);
            return;
        }

        private async Task CreateGame(Container container, Player player)
        {
            await Task.Delay(500);
            currentGame = new Game(player.Name);
            await container.CreateItemAsync<Game>(currentGame); //new PartitionKey()


        }
        /// <summary>
        /// Replace an item in the container
        /// </summary>
        //private async Task ReplaceFamilyItemAsync(Container container)
        //{
        //    ItemResponse<Game> wakefieldFamilyResponse = await container.UpsertItemAsync<Game>(currentGame);
        //    var itemBody = wakefieldFamilyResponse.Resource;
        //
        //    // update registration status from false to true
        //    itemBody.CreatorName = "Filip";
        //    // update grade of child
        //    
        //
        //    // replace the item with the updated content
        //    wakefieldFamilyResponse = await container.ReplaceItemAsync<Game>(itemBody, itemBody.Id);
        //    Console.WriteLine("Updated Family [{0},{1}].\n \tBody is now: {2}\n", itemBody.CreatorName, itemBody.Id, wakefieldFamilyResponse.Resource);
        //}
        private static async Task InitializeAsync(
            string databaseId,
            CosmosClient client)
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
    }

 
    internal class ChangeFeedObserver : IChangeFeedObserver
    {
        public Task CloseAsync(IChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            return Task.CompletedTask;
        }

        public Task OpenAsync(IChangeFeedObserverContext context)
        {
            return Task.CompletedTask;
        }

        public Task ProcessChangesAsync(IChangeFeedObserverContext context, IReadOnlyList<Microsoft.Azure.Documents.Document> docs, CancellationToken cancellationToken)
        {
            foreach (var doc in docs)
            {
                Console.WriteLine($"\t[OLD Processor] Detected operation for item with id {doc.Id}, created at {doc.GetPropertyValue<DateTime>("creationTime")}.");
            }

            return Task.CompletedTask;
        }
    }
}
        /// <summary>
        /// The delegate for the Estimator receives a numeric representation of items pending to be read.
        /// </summary>
        // <EstimationDelegate>
        //static async Task HandleEstimationAsync(long estimation, CancellationToken cancellationToken)
        //{
        //    if (estimation > 0)
        //    {
        //        Console.WriteLine($"\tEstimator detected {estimation} items pending to be read by the Processor.");
        //    }
        //
        //    await Task.Delay(0);
        //}
        // </EstimationDelegate>
        //public static async Task RunMigrationSample(
        //    string databaseId,
        //    CosmosClient client,
        //    IConfigurationRoot configuration)
        //{
        //    await Program.InitializeAsync(databaseId, client);
        //
        //    Console.WriteLine("Generating 10 items that will be picked up by the old Change Feed Processor library...");
        //    await Program.GenerateItems(10, client.GetContainer(databaseId, Program.monitoredContainer));
        //
        //    // This is how you would initialize the processor in V2
        //    // <ChangeFeedProcessorLibrary>
        //    ChangeFeedProcessorLibrary.DocumentCollectionInfo monitoredCollectionInfo = new ChangeFeedProcessorLibrary.DocumentCollectionInfo()
        //    {
        //        DatabaseName = databaseId,
        //        CollectionName = Program.monitoredContainer,
        //        Uri = new Uri(configuration["EndPointUrl"]),
        //        MasterKey = configuration["AuthorizationKey"]
        //    };
        //
        //    ChangeFeedProcessorLibrary.DocumentCollectionInfo leaseCollectionInfo = new ChangeFeedProcessorLibrary.DocumentCollectionInfo()
        //    {
        //        DatabaseName = databaseId,
        //        CollectionName = Program.leasesContainer,
        //        Uri = new Uri(configuration["EndPointUrl"]),
        //        MasterKey = configuration["AuthorizationKey"]
        //    };
        //
        //    ChangeFeedProcessorLibrary.ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorLibrary.ChangeFeedProcessorBuilder();
        //    var oldChangeFeedProcessor = await builder
        //        .WithHostName("consoleHost")
        //        .WithProcessorOptions(new ChangeFeedProcessorLibrary.ChangeFeedProcessorOptions
        //        {
        //            StartFromBeginning = true,
        //            LeasePrefix = "MyLeasePrefix"
        //        })
        //         .WithProcessorOptions(new ChangeFeedProcessorLibrary.ChangeFeedProcessorOptions()
        //         {
        //             MaxItemCount = 10,
        //             FeedPollDelay = TimeSpan.FromSeconds(1)
        //         })
        //        .WithFeedCollection(monitoredCollectionInfo)
        //        .WithLeaseCollection(leaseCollectionInfo)
        //        .WithObserver<ChangeFeedObserver>()
        //        .BuildAsync();
        //    // </ChangeFeedProcessorLibrary>
        //
        //    await oldChangeFeedProcessor.StartAsync();
        //
        //    // Wait random time for the delegate to output all messages after initialization is done
        //    await Task.Delay(5000);
        //    Console.WriteLine("Now we will stop the V2 Processor and start a V3 with the same parameters to pick up from the same state, press any key to continue...");
        //    Console.ReadKey();
        //    await oldChangeFeedProcessor.StopAsync();
        //
        //    Console.WriteLine("Generating 5 items that will be picked up by the new Change Feed Processor...");
        //    await Program.GenerateItems(5, client.GetContainer(databaseId, Program.monitoredContainer));
        //
        //    // This is how you would do the same initialization in V3
        //    // <ChangeFeedProcessorMigrated>
        //    Container leaseContainer = client.GetContainer(databaseId, Program.leasesContainer);
        //    Container monitoredContainer = client.GetContainer(databaseId, Program.monitoredContainer);
        //    ChangeFeedProcessor changeFeedProcessor = monitoredContainer
        //        .GetChangeFeedProcessorBuilder<ToDoItem>("MyLeasePrefix", Program.HandleChangesAsync)
        //            .WithInstanceName("consoleHost")
        //            .WithLeaseContainer(leaseContainer)
        //            .WithMaxItems(10)
        //            .WithPollInterval(TimeSpan.FromSeconds(1))
        //            .WithStartTime(DateTime.MinValue.ToUniversalTime())
        //            .Build();
        //    // </ChangeFeedProcessorMigrated>
        //
        //    await changeFeedProcessor.StartAsync();
        //
        //    // Wait random time for the delegate to output all messages after initialization is done
        //    await Task.Delay(5000);
        //    Console.WriteLine("Press any key to continue with the next demo...");
        //    Console.ReadKey();
        //    await changeFeedProcessor.StopAsync();
        //}

        /// <summary>
        /// StartTime will make the Change Feed Processor start processing changes at a particular point in time, all previous changes are ignored.
        /// </summary>
        /// <remarks>
        /// StartTime only works if the leaseContainer is empty or contains no leases for the particular processor name.
        /// </remarks>
        //public static async Task RunStartTimeChangeFeed(
        //    string databaseId,
        //    CosmosClient client)
        //{
        //    await Program.InitializeAsync(databaseId, client);
        //    Console.WriteLine("Generating 5 items that will not be picked up.");
        //    await Program.GenerateItems(5, client.GetContainer(databaseId, Program.monitoredContainer));
        //    Console.WriteLine($"Items generated at {DateTime.UtcNow}");
        //    // Generate a future point in time
        //    await Task.Delay(2000);
        //    DateTime particularPointInTime = DateTime.UtcNow;
        //
        //    Console.WriteLine("Generating 5 items that will be picked up by the delegate...");
        //    await Program.GenerateItems(5, client.GetContainer(databaseId, Program.monitoredContainer));
        //
        //    // <TimeInitialization>
        //    Container leaseContainer = client.GetContainer(databaseId, Program.leasesContainer);
        //    Container monitoredContainer = client.GetContainer(databaseId, Program.monitoredContainer);
        //    ChangeFeedProcessor changeFeedProcessor = monitoredContainer
        //        .GetChangeFeedProcessorBuilder<ToDoItem>("changeFeedTime", Program.HandleChangesAsync)
        //            .WithInstanceName("consoleHost")
        //            .WithLeaseContainer(leaseContainer)
        //            .WithStartTime(particularPointInTime)
        //            .Build();
        //    // </TimeInitialization>
        //
        //    Console.WriteLine($"Starting Change Feed Processor with changes after {particularPointInTime}...");
        //    await changeFeedProcessor.StartAsync();
        //    Console.WriteLine("Change Feed Processor started.");
        //
        //    // Wait random time for the delegate to output all messages after initialization is done
        //    await Task.Delay(5000);
        //    Console.WriteLine("Press any key to continue with the next demo...");
        //    Console.ReadKey();
        //    await changeFeedProcessor.StopAsync();
        //}
        //
        /// <summary>
        /// Reading the Change Feed since the beginning of time.
        /// </summary>
        /// <remarks>
        /// StartTime only works if the leaseContainer is empty or contains no leases for the particular processor name.
        /// </remarks>
        //public static async Task RunStartFromBeginningChangeFeed(
        //    string databaseId,
        //    CosmosClient client)
        //{
        //    await Program.InitializeAsync(databaseId, client);
        //    Console.WriteLine("Generating 10 items that will be picked up by the delegate...");
        //    await Program.GenerateItems(10, client.GetContainer(databaseId, Program.monitoredContainer));
        //
        //    // <StartFromBeginningInitialization>
        //    Container leaseContainer = client.GetContainer(databaseId, Program.leasesContainer);
        //    Container monitoredContainer = client.GetContainer(databaseId, Program.monitoredContainer);
        //    ChangeFeedProcessor changeFeedProcessor = monitoredContainer
        //        .GetChangeFeedProcessorBuilder<ToDoItem>("changeFeedBeginning", Program.HandleChangesAsync)
        //            .WithInstanceName("consoleHost")
        //            .WithLeaseContainer(leaseContainer)
        //            .WithStartTime(DateTime.MinValue.ToUniversalTime())
        //            .Build();
        //    // </StartFromBeginningInitialization>
        //
        //    Console.WriteLine($"Starting Change Feed Processor with changes since the beginning...");
        //    await changeFeedProcessor.StartAsync();
        //    Console.WriteLine("Change Feed Processor started.");
        //
        //    // Wait random time for the delegate to output all messages after initialization is done
        //    await Task.Delay(5000);
        //    Console.WriteLine("Press any key to continue with the next demo...");
        //    Console.ReadKey();
        //    await changeFeedProcessor.StopAsync();
        //}
        //
        /// <summary>
        /// Exposing progress with the Estimator.
        /// </summary>
        /// <remarks>
        /// The Estimator uses the same processorName and the same lease configuration as the existing processor to measure progress.
        /// </remarks>
        //public static async Task RunEstimatorChangeFeed(
        //    string databaseId,
        //    CosmosClient client)
        //{
        //    await Program.InitializeAsync(databaseId, client);
        //
        //    // <StartProcessorEstimator>
        //    Container leaseContainer = client.GetContainer(databaseId, Program.leasesContainer);
        //    Container monitoredContainer = client.GetContainer(databaseId, Program.monitoredContainer);
        //    ChangeFeedProcessor changeFeedProcessor = monitoredContainer
        //        .GetChangeFeedProcessorBuilder<ToDoItem>("changeFeedEstimator", Program.HandleChangesAsync)
        //            .WithInstanceName("consoleHost")
        //            .WithLeaseContainer(leaseContainer)
        //            .Build();
        //    // </StartProcessorEstimator>
        //
        //    Console.WriteLine($"Starting Change Feed Processor...");
        //    await changeFeedProcessor.StartAsync();
        //    Console.WriteLine("Change Feed Processor started.");
        //
        //    Console.WriteLine("Generating 10 items that will be picked up by the delegate...");
        //    await Program.GenerateItems(10, client.GetContainer(databaseId, Program.monitoredContainer));
        //
        //    // Wait random time for the delegate to output all messages after initialization is done
        //    await Task.Delay(5000);
        //
        //    // <StartEstimator>
        //    ChangeFeedProcessor changeFeedEstimator = monitoredContainer
        //        .GetChangeFeedEstimatorBuilder("changeFeedEstimator", Program.HandleEstimationAsync, TimeSpan.FromMilliseconds(1000))
        //        .WithLeaseContainer(leaseContainer)
        //        .Build();
        //    // </StartEstimator>
        //
        //    Console.WriteLine($"Starting Change Feed Estimator...");
        //    await changeFeedEstimator.StartAsync();
        //    Console.WriteLine("Change Feed Estimator started.");
        //
        //    Console.WriteLine("Generating 10 items that will be picked up by the delegate and reported by the Estimator...");
        //    await Program.GenerateItems(10, client.GetContainer(databaseId, Program.monitoredContainer));
        //
        //    Console.WriteLine("Press any key to continue with the next demo...");
        //    Console.ReadKey();
        //    await changeFeedProcessor.StopAsync();
        //    await changeFeedEstimator.StopAsync();
        //}
        //
        /// <summary>
        /// Example of a code migration template from Change Feed Processor V2 to SDK V3.
        /// </summary>
        /// <returns></returns>