using Headless.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Headless.AtrapalhanciaHandler
{
    public class OverlayService : WebSocketBehavior
    {
        Random rnd = new Random();
        public Action<OverlayService> OnOverlayConnected;
        private string channel;

        public OverlayService(Action<OverlayService> onOverlayConnected, string channel)
        {
            OnOverlayConnected = onOverlayConnected;
            this.channel = channel;
        }


        string alienPic = "https://store.playstation.com/store/api/chihiro/00_09_000/container/US/en/19/UP2131-PCSE00625_00-RYMANEVILALIE15V/image?w=320&h=320&bg_color=000000&opacity=100&_version=00_09_000";
        protected override void OnMessage(MessageEventArgs e)
        {
            //TODO mover essa logica pro alienInvasion po...
            StringBuilder output = new StringBuilder();
            var alienLetters = new char[] { '#', '@', '&', 'ç', 'Ç', '~', 'x', '_', '0', '1', 'z', 'B' };
            var letterCount = rnd.Next(3, 10);
            for (int i = 0; i < letterCount; i++)
            {
                output.Append(alienLetters[rnd.Next(alienLetters.Length)]);
            }
            
            if (!e.IsPing)
            {
                if(e.Data == "alienblast")
                {
                    External.SendToBroadcaster[channel]($"atrapalhancia/random/{output.ToString()}/{alienPic.Replace("/", "%2F")}");
                }
            }
        }

        protected override void OnOpen()
        {
            OnOverlayConnected(this);
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {

        }
    }

    public class GameService : WebSocketBehavior //TODO have an even headerless class to handle atrapalhancias and implement that here
    {
        public Action<GameService> OnGameConnectionChanged;
        public bool Connected = false;
        private string channel;

        public GameService(Action<GameService> onGameConnectionChanged, string channel)
        {
            OnGameConnectionChanged = onGameConnectionChanged;
            this.channel = channel;
        }

        public List<string> ConnectedGames = new List<string>();

        protected override void OnMessage(MessageEventArgs e)
        {
            if(!e.IsPing)
            {
                var messageArgs = e.Data.Split('/');

                if (ConnectedGames.Contains(ID))
                {
                    if (messageArgs[0] == "banana")
                    {
                        if(External.SendToOverlay.TryGetValue(channel, out var SendToOverlay))
                        {
                            SendToOverlay(Encoding.UTF8.GetBytes(@"
                                {""event_name"": ""banana""}
                            "));
                        }
                    }
                }
                else
                {
                    if (messageArgs[2] != "v2")
                    {
                        Context.WebSocket.Send("mismatch");
                        Context.WebSocket.Close();
                    }
                    if (messageArgs.Length == 0) Context.WebSocket.Close();

                    if (messageArgs[0] == "game")
                    {
                        var gameName = messageArgs[1].Replace(".dll", "").ToLower();
                        if (File.Exists(Environment.CurrentDirectory + $"/Atrapalhancias/{gameName}.dll"))
                        {
                            //TODO quem conectou?
                            Connected = true;
                            OnGameConnectionChanged(this);
                            ConnectedGames.Add(ID);
                            Context.WebSocket.Send($"conectado/{this.ID}");
                        }
                    }
                }
            }
        }

        public void SendMessage(string message)
        {
            this.Send(message);
        }

        protected override void OnOpen()
        {

        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            Connected = false;
            OnGameConnectionChanged(this);

        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {

        }
    }

    public class StreamDeckService : WebSocketBehavior
    {
        public OBSWebsocket obsSocket;
        private string channel;

        public StreamDeckService(OBSWebsocket obsSocket, string channel)
        {
            this.obsSocket = obsSocket;
            this.channel = channel;
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (!e.IsPing)
            {
                var keys = e.Data.Split("+");
                if (keys.Length >= 2)
                {

                    if (keys[0] == "app" && keys[1] == "b")
                    {
                        External.SendToOverlay[channel](Encoding.UTF8.GetBytes(@"
                            {""event_name"": ""boom""}
                        "));
                    }

                    if (keys[0] == "app" && keys[1] == "s")
                    {
                        External.OnScreenshotCaptured = new Action<string>((_) =>
                        {
                            External.SendToOverlay[channel](Encoding.UTF8.GetBytes($@"
                                {{""event_name"": ""deepfriedscreenshot"", ""text"": ""{keys[2]}""}}
                            "));
                        });

                        obsSocket.SendRequest("CallVendorRequest", JsonConvert.DeserializeObject<JObject>(@"{
                                ""requestData"": {
                                    ""message"": ""screenshot""
                                },
                                ""requestType"": ""AdvancedSceneSwitcherMessage"",
                                ""vendorName"": ""AdvancedSceneSwitcher""
                            }")
                        );
                    }
                }
            }
        }

        protected override void OnOpen()
        {
            Console.WriteLine("StreamDeck connected!");
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {

        }
    }

    public class OBSService : WebSocketBehavior
    {

        protected override void OnMessage(MessageEventArgs e)
        {
            if (!e.IsPing)
            {
                
            }
        }

        protected override void OnOpen()
        {
            Console.WriteLine("OBS connected!");
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {

        }
    }

    public class WebsocketAtrapalhanciasServer : WebSocketServer
    {
       public WebsocketAtrapalhanciasServer(string url) : base(url)
       {

       }

        public bool RoomExists(string room)
        {
            return WebSocketServices[room] == null;
        }

        //public void AddRoom(string room, Action<GameService, string> OnGameConnected)
        //{
        //    AddWebSocketService("/channel/" + room, () => new GameService(OnGameConnected));
        //    AddWebSocketService("/channel/" + room + "/overlay", () => new OverlayService());
        //}
    }

    public class WebSocketServerManager
    {
        public WebsocketAtrapalhanciasServer server;
        public OBSWebsocket obsSocket;

        public WebSocketServerManager()
        {
            server = new WebsocketAtrapalhanciasServer("ws://localhost:5000");
            //obsSocket = new OBSWebsocket();
            //obsSocket.ConnectAsync("ws://localhost:4455", null);
        }

        public void CreateChannelServices(string channel, Action<GameService> onGameConnectionChanged, Action<OverlayService> onOverlayConnected)
        {
            //TODO better way to handle this null check (if it even can happen in prod)
            if (server.WebSocketServices[$"/ws/channel/{channel}"] == null)
            {
                server.AddWebSocketService("/ws/channel/" + channel, () => new GameService(onGameConnectionChanged, channel));
                server.AddWebSocketService("/ws/channel/" + channel + "/overlay", () => new OverlayService(onOverlayConnected, channel));
                server.AddWebSocketService("/ws/channel/" + channel + "/streamdeck", () => new StreamDeckService(obsSocket, channel));
                //server.AddWebSocketService("/ws/channel/" + channel + "/obs", () => new OBSService());
            }
        }

        public void Initialize()
        {
            if (server != null && server.IsListening) server.Stop();

            server.Start();
        }
    }
}
