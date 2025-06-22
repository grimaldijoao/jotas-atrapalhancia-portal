using AtrapalhanciaDatabase.Tables;
using Headless.AtrapalhanciaHandler;
using Headless.Shared;
using System;
using TwitchHandler;

namespace JotasAtrapalhanciaPortal
{
    internal class Program
    {
        private static Dictionary<string, Twitch> TwitchListeners = new Dictionary<string, Twitch>();
        private static TwitchAtrapalhanciaBuilder twitchAtrapalhanciaBuilder = new TwitchAtrapalhanciaBuilder();

        static void Main(string[] args)
        {
            var socketManager = new WebSocketServerManager();
            socketManager.Initialize();

            HttpServer.OnSocketCreationRequested += (sender, guid) =>
            {
                var gameBehavior = new GameBehavior($"/channel/{guid}");

                gameBehavior.OnConnectionOpen += (_, _) =>
                {
                   
                };

                gameBehavior.OnConnectionClosed += (_, _) =>
                {
                    Console.WriteLine($"{gameBehavior.ChannelName ?? guid} disconnected!");

                    if(gameBehavior.ChannelName != null)
                    {

                        if (TwitchListeners.ContainsKey(gameBehavior.ChannelName))
                        {
                            TwitchListeners[gameBehavior.ChannelName].Dispose();
                            TwitchListeners.Remove(gameBehavior.ChannelName);
                            TwitchReward.DeleteRewardsByChannelName(gameBehavior.ChannelName);
                        }
                    } 
                };

                socketManager.SocketServer.AddService(gameBehavior);
            };

            HttpServer.OnGameConnected += (sender, args) =>
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

                    var atrapalhancias = twitchAtrapalhanciaBuilder.BuildAtrapalhanciasFromFile(Path.Combine(Environment.CurrentDirectory, "Atrapalhancias", $"{args.Game}.dll"));
                    var rewards = TwitchListeners[channel].CreateRedeemRewardsAsync(atrapalhancias).GetAwaiter().GetResult();

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

                External.SendToBroadcaster[channel] = (message) => socketManager.SocketServer.BroadcastServiceAsync(args.Route, message);
                Console.WriteLine($"{args.Route} sendToBroadcaster registered!");
            };

            HttpServer.Run(new FirebaseAuthHandler(), socketManager.SocketServer);

            Console.ReadLine();

        }
    }
}
