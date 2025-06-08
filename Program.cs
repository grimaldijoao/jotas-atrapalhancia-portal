using Headless.AtrapalhanciaHandler;
using Headless.Shared;
using ScreenshotHandler;
using TwitchHandler;

namespace JotasAtrapalhanciaPortal
{
    internal class Program
    {
        private static Dictionary<string, Twitch> TwitchListeners = new Dictionary<string, Twitch>();

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
                        }
                        else
                        {
                            Console.WriteLine($"{channel} disconnected!");
                            TwitchListeners.Remove(channel);
                        }
                    },
                    (sender) =>
                    {
                        External.SendToOverlay[channel] = socketManager.server.WebSocketServices[$"/channel/{channel}/overlay/"].Sessions.Broadcast;
                    });

                var twitchConnection = new Twitch(channel);
                twitchConnection.Connect();

                TwitchListeners.Add(channel, twitchConnection);
            };

            HttpServer.OnGameConnected += (sender, args) => 
            {
                if (TwitchListeners.ContainsKey(args.Channel))
                {
                    var rewards = TwitchAtrapalhanciaBuilder.BuildRewardsFromFile(Environment.CurrentDirectory + $"/Atrapalhancias/{args.Game}.dll");
                    TwitchListeners[args.Channel].CreateRedeemRewards(rewards);
                }
            };

            HttpServer.Run(socketManager.server, new FirebaseAuthHandler());

            Console.ReadLine();

        }
    }
}
