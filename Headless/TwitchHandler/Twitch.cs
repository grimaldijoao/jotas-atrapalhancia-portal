using Headless.Shared;
using InvasionHandler;
using JotasTwitchPortal.JSON;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace TwitchHandler
{
    public class Twitch : IDisposable
    {
        private string ChannelName;

        string clientId = "gp762nuuoqcoxypju8c569th9wz7q5";
        string userId = "235332563"; //broadcaster id
        string bot_access_token = File.ReadAllText("bot_access_token.txt");
        string access_token = File.ReadAllText("access_token.txt");
        TwitchClient client;
        TwitchAPI api;
        EventSub eventSub;

        private AlienInvasion Invasion = new AlienInvasion(); //TODO module adder instead of everyone having this? lol

        private Dictionary<string, User> ConnectedUsers = new Dictionary<string, User>();

        public Twitch(string channel)
        {
            ChannelName = channel;
        }

        public void Connect()
        {
            ConnectionCredentials credentials = new ConnectionCredentials("umbotas", bot_access_token);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };

            client = new TwitchClient(new WebSocketClient(clientOptions));
            client.Initialize(credentials, ChannelName);

            api = new TwitchAPI();

            api.Settings.AccessToken = bot_access_token;
            api.Settings.ClientId = clientId;

            client.OnLog += Client_OnLog;
            client.OnUserJoined += Client_OnUserJoined;
            client.OnJoinedChannel += Client_OnJoinedChannel;
            client.OnMessageReceived += Client_OnMessageReceived;


            client.Connect();

            eventSub = new EventSub();
            eventSub.OnChatRewardRedeemed += EventSub_OnChatRewardRedeemed;
            eventSub.Connect();

            Task.Factory.StartNew(() =>
            {
                while (eventSub.SessionId == null)
                {
                    Task.Delay(500).Wait();
                }
                
                var response = api.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_custom_reward_redemption.add", "1", new Dictionary<string, string>() { { "broadcaster_user_id", "235332563" } }, TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket, eventSub.SessionId, null, null, clientId, access_token).GetAwaiter().GetResult();
                
            });

            Console.WriteLine($"{ChannelName} connected!");
        }

        public void SendChatMessage(string message)
        {
            client.SendMessage(client.JoinedChannels.ElementAt(0), message);
        }

        private User TryFirstJoin(string username)
        {
            if(ConnectedUsers.ContainsKey(username))
            {
                return ConnectedUsers[username];
            }

            var userData = api.Helix.Users.GetUsersAsync(logins: new List<string>() { username }).GetAwaiter().GetResult().Users[0];
            Console.WriteLine(username, "portou");

            if (External.SendToOverlay.TryGetValue(ChannelName, out var SendToOverlay))
            {
                SendToOverlay(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                {
                    username = userData.DisplayName,
                    profile_pic = userData.ProfileImageUrl,
                    event_name = "porta"
                })));
            }

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
                    user.Atrapalhate(ChannelName, TwitchRewardsAtrapalhancias[rewardEvent.Event.Reward.Id]);
                }
                else
                {
                    user = TryFirstJoin(rewardEvent.Event.UserLogin);
                    user.Atrapalhate(ChannelName, TwitchRewardsAtrapalhancias[rewardEvent.Event.Reward.Id]);
                }

                //TODO sistema de verdade
                if(rewardEvent.Event.Reward.Title == "Nao pode pular")
                {
                    External.SendToOverlay[ChannelName](Encoding.UTF8.GetBytes(@"
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

            Task.Factory.StartNew(() =>
            {
                while (client.JoinedChannels.Count == 0)
                {
                    Task.Delay(500).Wait();
                    //? I dont know why this occasionally happens, TwitchLib is sus...
                }
                SendChatMessage("🤖🤝👽");
            });
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

        public void Dispose()
        {
            api = null;

            eventSub.Dispose();
            client.Disconnect();
            client = null;
        }
    }
}
