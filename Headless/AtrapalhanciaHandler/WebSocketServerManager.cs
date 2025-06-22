using AtrapalhanciaWebSocket;
using Headless.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using System.Text;
using TwitchLib.Api.Helix.Models.Moderation.CheckAutoModStatus;

namespace Headless.AtrapalhanciaHandler
{
	//public class OverlayService : WebSocketBehavior
	//{
	//    Random rnd = new Random();
	//    public Action<OverlayService> OnOverlayConnected;
	//    private string channel;
	//
	//    public OverlayService(Action<OverlayService> onOverlayConnected, string channel)
	//    {
	//        OnOverlayConnected = onOverlayConnected;
	//        this.channel = channel;
	//    }
	//
	//
	//    string alienPic = "https://store.playstation.com/store/api/chihiro/00_09_000/container/US/en/19/UP2131-PCSE00625_00-RYMANEVILALIE15V/image?w=320&h=320&bg_color=000000&opacity=100&_version=00_09_000";
	//    protected override void OnMessage(MessageEventArgs e)
	//    {
	//        //TODO mover essa logica pro alienInvasion po...
	//        StringBuilder output = new StringBuilder();
	//        var alienLetters = new char[] { '#', '@', '&', 'ç', 'Ç', '~', 'x', '_', '0', '1', 'z', 'B' };
	//        var letterCount = rnd.Next(3, 10);
	//        for (int i = 0; i < letterCount; i++)
	//        {
	//            output.Append(alienLetters[rnd.Next(alienLetters.Length)]);
	//        }
	//        
	//        if (!e.IsPing)
	//        {
	//            if(e.Data == "alienblast")
	//            {
	//                External.SendToBroadcaster[channel]($"atrapalhancia/random/{output.ToString()}/{alienPic.Replace("/", "%2F")}");
	//            }
	//        }
	//    }
	//
	//    protected override void OnOpen()
	//    {
	//        OnOverlayConnected(this);
	//    }
	//
	//    protected override void OnError(WebSocketSharp.ErrorEventArgs e)
	//    {
	//
	//    }
	//}

	public class GameBehavior : BaseServiceBehavior
	{
		public event EventHandler OnConnectionOpen;
        public event EventHandler OnConnectionClosed;

        public GameBehavior(string route) : base(route)
		{
		}

		protected override void OnOpen()
		{
			OnConnectionOpen.Invoke(this, EventArgs.Empty);

        }

		protected override void OnClose()
		{
            OnConnectionClosed.Invoke(this, EventArgs.Empty);

        }

		protected override void OnMessage(string message)
		{
			Console.WriteLine($"Message received: {message}");
			var messageArgs = message.Split('/');

            if (messageArgs.Length == 0)
            {
                Close();
                return;
            }

            if (messageArgs[2] != "v2")
			{
				SendAsync("mismatch");
				Close();
				return;
			}

			if (messageArgs[0] == "game")
			{
				var gameName = messageArgs[1].Replace(".dll", "");
				if (File.Exists(Path.Combine(Environment.CurrentDirectory, $"Atrapalhancias/{gameName}.dll")))
				{
					SendAsync($"conectado/test");
				}
				else
				{
					Console.WriteLine($"Atrapalhancia game not found! {Path.Combine(Environment.CurrentDirectory, $"Atrapalhancias/{gameName}.dll")}");
                    Close();
                }
			}
		}
	}

   //public class StreamDeckService : WebSocketBehavior
   //{
   //    public OBSWebsocket obsSocket;
   //    private string channel;
   //
   //    public StreamDeckService(OBSWebsocket obsSocket, string channel)
   //    {
   //        this.obsSocket = obsSocket;
   //        this.channel = channel;
   //    }
   //
   //    protected override void OnMessage(MessageEventArgs e)
   //    {
   //        if (!e.IsPing)
   //        {
   //            var keys = e.Data.Split("+");
   //            if (keys.Length >= 2)
   //            {
   //
   //                if (keys[0] == "app" && keys[1] == "b")
   //                {
   //                    External.SendToOverlay[channel](Encoding.UTF8.GetBytes(@"
   //                        {""event_name"": ""boom""}
   //                    "));
   //                }
   //
   //                if (keys[0] == "app" && keys[1] == "s")
   //                {
   //                    External.OnScreenshotCaptured = new Action<string>((_) =>
   //                    {
   //                        External.SendToOverlay[channel](Encoding.UTF8.GetBytes($@"
   //                            {{""event_name"": ""deepfriedscreenshot"", ""text"": ""{keys[2]}""}}
   //                        "));
   //                    });
   //
   //                    obsSocket.SendRequest("CallVendorRequest", JsonConvert.DeserializeObject<JObject>(@"{
   //                            ""requestData"": {
   //                                ""message"": ""screenshot""
   //                            },
   //                            ""requestType"": ""AdvancedSceneSwitcherMessage"",
   //                            ""vendorName"": ""AdvancedSceneSwitcher""
   //                        }")
   //                    );
   //                }
   //            }
   //        }
   //    }
   //
   //    protected override void OnOpen()
   //    {
   //        Console.WriteLine("StreamDeck connected!");
   //    }
   //
   //    protected override void OnError(WebSocketSharp.ErrorEventArgs e)
   //    {
   //
   //    }
   //}

    public class WebSocketServerManager
    {
        public WebSocketServer SocketServer;

        public WebSocketServerManager()
        {
        }

        public void CreateChannelServices(string channel)
        {
            if (!SocketServer.ServiceExists($"/channel/${channel}"))
            {
				SocketServer.AddService(new GameBehavior($"/channel/${channel}"));
                //server.AddWebSocketService("/channel/" + channel, () => new GameService(onGameConnectionChanged, channel));
                //server.AddWebSocketService("/channel/" + channel + "/overlay", () => new OverlayService(onOverlayConnected, channel));
                //server.AddWebSocketService("/channel/" + channel + "/streamdeck", () => new StreamDeckService(obsSocket, channel));
                //server.AddWebSocketService("/channel/" + channel + "/obs", () => new OBSService());

                Console.WriteLine("Websocket registered: /channel/" + channel);
            }
        }

        public void Initialize()
        {
			SocketServer = WebSocketServer.Listen();
        }
    }
}
