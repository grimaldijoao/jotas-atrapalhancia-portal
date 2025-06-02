using Headless.AtrapalhanciaHandler;
using Headless.AtrapalhanciaHandler.Shared;
using ScreenshotHandler;
using Shared;
using TwitchLib.Api.Helix.Models.Games;

namespace JotasTwitchPortal
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var channelName = "umjotas";
            var socketManager = new WebSocketServerManager();

            var portal = new TwitchPortal(ref socketManager.server, channelName);
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

            HttpServer.Run(ref socketManager.server, portal);

            Console.ReadLine();

        }
    }
}
