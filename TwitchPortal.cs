using Headless.AtrapalhanciaHandler;
using Headless.AtrapalhanciaHandler.Shared;
using InvasionHandler;
using JotasTwitchPortal.JSON;
using Newtonsoft.Json;
using Shared;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using TwitchLib.Api;
using TwitchLib.Api.Helix;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.EventSub.Websockets;
using TwitchLib.PubSub;

namespace JotasTwitchPortal
{
    //TODO renomear e fazer os bots do renejotas
    public class TwitchPortal : ITwitchPortal
    {
        private string ChannelName;

        string clientId = "gp762nuuoqcoxypju8c569th9wz7q5";
        string userId = "235332563"; //broadcaster id
        string bot_access_token = File.ReadAllText("bot_access_token.txt");
        string access_token = File.ReadAllText("access_token.txt");
        TwitchClient client;
        TwitchPubSub clientPubSub;
        TwitchAPI api;

        private readonly EventSubWebsocketClient _eventSubWebsocketClient;

        private AlienInvasion Invasion = new AlienInvasion(); //TODO module adder instead of everyone having this? lol

        private WebsocketAtrapalhanciasServer SocketServer; //TODO depreciar lentamente pra não expor essa camada
        private Dictionary<string, User> ConnectedUsers = new Dictionary<string, User>();

        public TwitchPortal(ref WebsocketAtrapalhanciasServer socketServer, string channel)
        {
            ChannelName = channel;
            SocketServer = socketServer;
        }

        public void Connect()
        {
            ConnectionCredentials credentials = new ConnectionCredentials("umbotas", bot_access_token);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };

            WebSocketClient customClient = new WebSocketClient(clientOptions);

            customClient.OnData += CustomClient_OnData;
            customClient.OnMessage += CustomClient_OnMessage;

            client = new TwitchClient(customClient);
            client.Initialize(credentials, ChannelName);

            api = new TwitchAPI();

            api.Settings.AccessToken = bot_access_token;
            api.Settings.ClientId = clientId;

            client.OnLog += Client_OnLog;
            client.OnUserJoined += Client_OnUserJoined;
            client.OnJoinedChannel += Client_OnJoinedChannel;
            client.OnMessageReceived += Client_OnMessageReceived;
            client.OnWhisperReceived += Client_OnWhisperReceived;
            client.OnNewSubscriber += Client_OnNewSubscriber;
            client.OnConnected += Client_OnConnected;


            client.Connect();

            var eventSub = new EventSub();
            eventSub.OnChatRewardRedeemed += EventSub_OnChatRewardRedeemed;
            eventSub.Connect();

            Task.Factory.StartNew(() =>
            {
                while (EventSub.SessionId == null)
                {
                    Task.Delay(500).Wait();
                }
                
                var response = api.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_custom_reward_redemption.add", "1", new Dictionary<string, string>() { { "broadcaster_user_id", "235332563" } }, TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket, EventSub.SessionId, null, null, clientId, access_token).GetAwaiter().GetResult();
                
            });
        }

        public void SendChatMessage(string message)
        {
            client.SendMessage(client.JoinedChannels.ElementAt(0), message);
        }

        private void CustomClient_OnMessage(object? sender, TwitchLib.Communication.Events.OnMessageEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        private void CustomClient_OnData(object? sender, TwitchLib.Communication.Events.OnDataEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private User TryFirstJoin(string username)
        {
            if(ConnectedUsers.ContainsKey(username))
            {
                return ConnectedUsers[username];
            }

            var userData = api.Helix.Users.GetUsersAsync(logins: new List<string>() { username }).GetAwaiter().GetResult().Users[0];
            Console.WriteLine(username, "portou");
            SocketServer.WebSocketServices[$"/channel/{ChannelName}/overlay"].Sessions.Broadcast(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
            {
                username = userData.DisplayName,
                profile_pic = userData.ProfileImageUrl,
                event_name = "porta"
            })));

            var user = new User(username, userData.ProfileImageUrl);

            ConnectedUsers[username] = user;

            return user;
        }

        ConcurrentDictionary<string, string> TwitchRewardsAtrapalhancias = new ConcurrentDictionary<string, string>();

        public void CreateRedeemRewards(Dictionary<string, CreateCustomRewardsRequest> atrapalhanciaRewards)
        {
            var rewards = api.Helix.ChannelPoints.GetCustomRewardAsync(userId, null, true, access_token).GetAwaiter().GetResult();
            
            var tasks = new List<Task>();
            
            foreach(var reward in rewards.Data)
            {
                api.Helix.ChannelPoints.DeleteCustomRewardAsync(userId, reward.Id, access_token).GetAwaiter().GetResult();
            }

            Task.WaitAll(tasks.ToArray());

            tasks.Clear();
            
            foreach (var reward in atrapalhanciaRewards)
            {
                tasks.Add(
                    api.Helix.ChannelPoints.CreateCustomRewardsAsync(userId, reward.Value, access_token).ContinueWith((customReward) =>
                    {
                        TwitchRewardsAtrapalhancias[customReward.Result.Data.First().Id] = reward.Key;
                    })
                );
            }
            
            Task.WaitAll(tasks.ToArray());
        }

        private void EventSub_OnChatRewardRedeemed(object sender, RewardEvent rewardEvent)
        {
            string atrapalhancia;
            User user;

            if(TwitchRewardsAtrapalhancias.TryGetValue(rewardEvent.Event.Reward.Id, out atrapalhancia))
            {
                if (ConnectedUsers.TryGetValue(rewardEvent.Event.UserLogin, out user))
                {
                    user.Atrapalhate(TwitchRewardsAtrapalhancias[rewardEvent.Event.Reward.Id]);
                }
                else
                {
                    user = TryFirstJoin(rewardEvent.Event.UserLogin);
                    user.Atrapalhate(TwitchRewardsAtrapalhancias[rewardEvent.Event.Reward.Id]);
                }

                //TODO sistema de verdade
                if(rewardEvent.Event.Reward.Title == "Nao pode pular")
                {
                    External.SendToOverlay(Encoding.UTF8.GetBytes(@"
                        {""event_name"": ""jumpTimer"", ""label"": ""Não pode pular!"", ""seconds"": 5}
                    "));
                }
            }

            if (rewardEvent.Event.Reward.Title == "Deletar coisas")
            {
                Process scriptProc = new Process();
                scriptProc.StartInfo.FileName = @"cscript";
                scriptProc.StartInfo.Arguments = "//B //Nologo backspace.vbs";
                scriptProc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                scriptProc.Start();
            }
        }

        private void Client_OnLog(object sender, OnLogArgs e)
        {
            Console.WriteLine($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {        

        }

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            var userData = api.Helix.Users.GetUsersAsync(logins: new List<string>() { e.Channel, "umbotas" }).GetAwaiter().GetResult().Users;

            //External.SendToOverlay(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
            //{
            //    username = e.Channel,
            //    profile_pic = "https://static-cdn.jtvnw.net/jtv_user_pictures/f2ce0467-b3d6-4780-927a-8c38cd0bed0f-profile_image-70x70.png",
            //    event_name = "porta"
            //})));

            Console.WriteLine($"Connected to twitch channel {e.Channel}!");

            //SocketServer.AddRoom(e.Channel, (sender, game) =>
            //{
            //    GameService service = sender;
            //    BroadcasterSocket = service.Context.WebSocket;
            //
            //    var rewards = new TwitchAtrapalhanciaBuilder().BuildRewardsFromFile(Environment.CurrentDirectory + $"/Atrapalhancias/{game}.dll");
            //    CreateRedeemRewards(rewards);
            //});

            client.SendMessage(e.Channel, "🤖🤝👽");
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            var messageSplit = e.ChatMessage.Message.ToLower().Split(' ');
            foreach (var word in messageSplit)
            {
                if(word.Contains("rene"))
                {
                    Console.WriteLine("Rene Mentioned!");
                    Invasion.ReneMentioned();
                    break;
                }
            }

            TryFirstJoin(e.ChatMessage.Username);
        }

        private void Client_OnUserJoined(object sender, OnUserJoinedArgs e)
        {
            TryFirstJoin(e.Username);
        }

        private void Client_OnWhisperReceived(object sender, OnWhisperReceivedArgs e)
        {

        }

        private void Client_OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            
        }
    }
}
