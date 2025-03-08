using Headless.AtrapalhanciaHandler;
using Headless.AtrapalhanciaHandler.Shared;
using ScreenshotHandler;
using TwitchLib.Api.Helix.Models.Games;

namespace JotasTwitchPortal
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var channelName = "umjotas";
            var socketManager = new WebSocketServerManager();

            var bot = new Bot(ref socketManager.server, channelName);
            bot.Connect();

            socketManager.Initialize(channelName, (sender, gameName) => //TODO race condition?
            {
                GameService service = sender;
                External.SendToBroadcaster = socketManager.server.WebSocketServices[$"/channel/{channelName}/"].Sessions.Broadcast;

                var rewards = new TwitchAtrapalhanciaBuilder().BuildRewardsFromFile(Environment.CurrentDirectory + $"/Atrapalhancias/{gameName}.dll");
                bot.CreateRedeemRewards(rewards);
            }, (sender) =>
            {
                External.SendToOverlay = socketManager.server.WebSocketServices[$"/channel/{channelName}/overlay/"].Sessions.Broadcast;
            });


            var screenshotListener = new ScreenshotListener();
            screenshotListener.Init();

            HttpServer.Run(ref socketManager.server);

            Console.ReadLine();

        }
    }
}
