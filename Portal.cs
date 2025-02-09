using HeadlessAtrapalhanciaHandler;
using Newtonsoft.Json.Linq;
using System.Text;
using WebSocketSharp.Server;

namespace JotasTwitchPortal
{
    public class Portal
    {
        WebSocketServer socketServer;

        public void Send(JObject json)
        {
            socketServer.WebSocketServices.Broadcast(Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None)));
        }

        public WebSocketServer Run()
        {
            socketServer = WebSocket.Initialize();

            var user = new User(Send, "jotas-usuario-fake");

            user.Atrapalhate(new AtrapalhanciaPayload("jump", null));

            return socketServer;
        }
    }
}
