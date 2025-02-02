using HeadlessAtrapalhanciaHandler;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.PubSub.Models.Responses.Messages.AutomodCaughtMessage;
using WebSocketSharp.Server;

namespace JotasTwitchPortal
{
    public class Portal
    {
        WebSocketServiceManager socketServer;

        public void Send(JObject json)
        {
            socketServer.Broadcast(Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None)));
        }

        public WebSocketServiceManager Run()
        {
            socketServer = WebSocket.Initialize();

            var user = new User(Send, "jotas-usuario-fake");

            user.Atrapalhate(new AtrapalhanciaPayload("jump", null));

            return socketServer;
        }
    }
}
