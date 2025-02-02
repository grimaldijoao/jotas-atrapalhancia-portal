using WebSocketSharp;
using WebSocketSharp.Server;

namespace JotasTwitchPortal
{
    public class MainService : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {

        }
    }

    public class WebSocket
    {
        public static WebSocketServiceManager Initialize()
        {
            var server = new WebSocketServer("ws://localhost:5000");

            server.AddWebSocketService<MainService>("/");
            server.Start();

            return server.WebSocketServices;
        }
    }
}
