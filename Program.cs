using Headless.AtrapalhanciaHandler;
using Headless.Shared;
using ScreenshotHandler;
using TwitchHandler;

namespace JotasTwitchPortal
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var channelName = "umjotas";
            var socketManager = new WebSocketServerManager();

            var portal = new Twitch(channelName);
            portal.Connect();

            socketManager.Initialize(channelName, (sender, gameName) => //TODO race condition?
            {
                GameService service = sender;
                External.SendToBroadcaster = socketManager.server.WebSocketServices[$"/channel/{channelName}/"].Sessions.Broadcast;
            }, (sender) =>
            {
                External.SendToOverlay = socketManager.server.WebSocketServices[$"/channel/{channelName}/overlay/"].Sessions.Broadcast;
            });


            var screenshotListener = new ScreenshotListener();
            screenshotListener.Init();

            HttpServer.OnGameConnected += (sender, game) => 
            {
                var rewards = TwitchAtrapalhanciaBuilder.BuildRewardsFromFile(Environment.CurrentDirectory + $"/Atrapalhancias/{game}.dll");
                portal.CreateRedeemRewards(rewards);
            };

            HttpServer.Run(socketManager.server, new FirebaseAuthHandler());

            Console.ReadLine();

        }
    }
}
