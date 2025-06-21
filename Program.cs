using AtrapalhanciaDatabase.Tables;
using Headless.AtrapalhanciaHandler;
using Headless.Shared;
using System.Threading.Channels;
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

            //var screenshotListener = new ScreenshotListener();
            //screenshotListener.Init();

            HttpServer.OnSocketCreationRequested += (sender, channel) =>
            {
                socketManager.CreateChannelServices(channel,
                    (sender) =>
                    {
                        GameService service = sender;
                        if (service.Connected)
                        {
                            External.SendToBroadcaster[channel] = socketManager.server.WebSocketServices[$"/channel/{channel}/"].Sessions.Broadcast;
                            Console.WriteLine($"{channel} sendToBroadcaster registered!");
                        }
                        else
                        {
                            Console.WriteLine($"{channel} disconnected!");

                            if(TwitchListeners.ContainsKey(channel))
                            {
                                TwitchListeners[channel].Dispose();
                                TwitchListeners.Remove(channel);
                                TwitchReward.DeleteRewardsByChannelName(channel);
                            }
                        }
                    },
                    (sender) =>
                    {
                        External.SendToOverlay[channel] = socketManager.server.WebSocketServices[$"/channel/{channel}/overlay/"].Sessions.Broadcast;
                    });
            };

            HttpServer.OnGameConnected += (sender, args) => 
            {
                Console.WriteLine("Game Connected!");
                var channel = args.ChannelName;
                var broadcaster_id = args.BroadcasterId;
                var access_token = args.AccessToken;

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
                Console.WriteLine("Game Connected ok!");
            };

            HttpServer.Run(socketManager.server, new FirebaseAuthHandler());

            Console.ReadLine();

        }
    }
}
