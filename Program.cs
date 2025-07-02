using AtrapalhanciaDatabase.Tables;
using Headless.AtrapalhanciaHandler;
using Headless.Shared;
using TwitchHandler;

namespace JotasAtrapalhanciaPortal
{
    internal class Program
    {
        private static Dictionary<string, Twitch> TwitchListeners = new Dictionary<string, Twitch>();
        private static TwitchAtrapalhanciaBuilder TwitchAtrapalhanciaBuilder = new TwitchAtrapalhanciaBuilder();

        private static WebSocketServerManager SocketManager = new WebSocketServerManager();

        static void Main(string[] args)
        {
            SocketManager.Initialize();

            HttpServer.OnSocketCreationRequested += HttpServer_OnSocketCreationRequested;

            HttpServer.OnGameConnected += HttpServer_OnGameConnected;

            HttpServer.Run(new FirebaseAuthHandler(), SocketManager.SocketServer);

            Console.ReadLine();

        }

        private static void HttpServer_OnGameConnected(object? sender, GameConnectedEventArgs args)
        {
            var channel = args.ChannelName;
            var broadcaster_id = args.BroadcasterId;
            var access_token = args.AccessToken;
            var route = args.Route;

            try
            {
                if (!TwitchListeners.TryGetValue(channel, out var twitchConnection))
                {
                    TwitchListeners.Remove(channel);
                    twitchConnection = new Twitch(broadcaster_id, channel, access_token);

                    twitchConnection.OnAtrapalhanciaUserCreated += TwitchConnection_OnAtrapalhanciaUserCreated;

                    twitchConnection.Connect();
                    TwitchListeners.Add(channel, twitchConnection);
                }

                var dbRewards = TwitchReward.GetRewardsByChannelName(channel);

                foreach (var reward in dbRewards)
                {
                    if (TwitchListeners[channel].DeleteRedeemReward(reward.Id).GetAwaiter().GetResult())
                    {
                        reward.Delete();
                    }
                }

                var atrapalhancias = TwitchAtrapalhanciaBuilder.BuildAtrapalhanciasFromFile(Path.Combine(Environment.CurrentDirectory, "Atrapalhancias", $"{args.Game}.dll"));
                var rewards = TwitchListeners[channel].CreateRedeemRewardsAsync(atrapalhancias).GetAwaiter().GetResult();
                //TODO se esse createredeem quebrar ele fica no loop infinito kkkkk. Acho que dá até pra resolver pegando tudo e deletando por nome?

                foreach (var rewardResponse in rewards)
                {
                    var reward = rewardResponse.Data.First();
                    var relation = TwitchRelation.GetInstance(channel);

                    if (relation == null) break;

                    TwitchReward.Create(reward.Id, reward.Title, relation.Id);
                }
            }
            catch (TwitchLib.Api.Core.Exceptions.BadRequestException e)
            {
                Console.WriteLine($"{channel} twitch connection failed! ({e.Message})");
                throw;
            }

            Console.WriteLine($"{args.Route} sendToBroadcaster registered!");
        }

        private static void HttpServer_OnSocketCreationRequested(object? sender, string guid)
        {
            var gameBehavior = new GameBehavior($"/channel/{guid}");

            gameBehavior.OnConnectionClosed += GameBehavior_OnConnectionClosed;

            SocketManager.SocketServer.AddService(gameBehavior);
        }

        private static void GameBehavior_OnConnectionClosed(object? sender, EventArgs e)
        {
            var gameBehavior = sender as GameBehavior;

            Console.WriteLine($"{gameBehavior.ChannelName ?? gameBehavior.Route} disconnected!");

            if (gameBehavior.ChannelName != null)
            {

                if (TwitchListeners.ContainsKey(gameBehavior.ChannelName))
                {
                    TwitchListeners[gameBehavior.ChannelName].Dispose();
                    TwitchListeners.Remove(gameBehavior.ChannelName);
                    TwitchReward.DeleteRewardsByChannelName(gameBehavior.ChannelName);
                }
            }
        }

        private static void TwitchConnection_OnAtrapalhanciaUserCreated(object? sender, User user)
        {
            var twitchListener = (Twitch)sender;
            user.LoadAtrapalhanciaSender((string message) => SocketManager.SocketServer.BroadcastServiceAsync($"{twitchListener.ChannelName}", message));
        }
    }
}
