using Headless.AtrapalhanciaHandler;
using Headless.AtrapalhanciaHandler.Shared;
using InvasionHandler;
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
using TwitchLib.PubSub;

namespace JotasTwitchPortal
{
    //TODO renomear e fazer os bots do renejotas
    public class Bot
    {
        private string ChannelName;

        string clientId = "gp762nuuoqcoxypju8c569th9wz7q5";
        string userId = "235332563"; //broadcaster id
        string bot_access_token = File.ReadAllText("bot_access_token.txt");
        string access_token = File.ReadAllText("access_token.txt");
        TwitchClient client;
        TwitchPubSub clientPubSub;
        TwitchAPI api;

        private AlienInvasion Invasion = new AlienInvasion(); //TODO module adder instead of everyone having this? lol

        private WebsocketAtrapalhanciasServer SocketServer; //TODO depreciar lentamente pra não expor essa camada
        private Dictionary<string, User> ConnectedUsers = new Dictionary<string, User>();

        public Bot(ref WebsocketAtrapalhanciasServer socketServer, string channel)
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


            clientPubSub = new TwitchPubSub();
            clientPubSub.OnPubSubServiceConnected += ClientPubSub_OnPubSubServiceConnected;
            clientPubSub.OnChannelPointsRewardRedeemed += ClientPubSub_OnChatRewardRedeemed;

            clientPubSub.Connect();
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
                tasks.Add(
                    api.Helix.ChannelPoints.DeleteCustomRewardAsync(userId, reward.Id, access_token)
                );
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

        private void ClientPubSub_OnPubSubServiceConnected(object sender, EventArgs e)
        {
            clientPubSub.ListenToChannelPoints(userId);
            clientPubSub.SendTopics(access_token);
            Console.WriteLine("Pubsub Abrido!");
        }

        private void ClientPubSub_OnChatRewardRedeemed(object? sender, TwitchLib.PubSub.Events.OnChannelPointsRewardRedeemedArgs e)
        {
            string atrapalhancia;
            User user;

            if(TwitchRewardsAtrapalhancias.TryGetValue(e.RewardRedeemed.Redemption.Reward.Id, out atrapalhancia))
            {
                if (ConnectedUsers.TryGetValue(e.RewardRedeemed.Redemption.User.Login, out user))
                {
                    user.Atrapalhate(TwitchRewardsAtrapalhancias[e.RewardRedeemed.Redemption.Reward.Id]);
                }
                else
                {
                    user = TryFirstJoin(e.RewardRedeemed.Redemption.User.Login);
                    user.Atrapalhate(TwitchRewardsAtrapalhancias[e.RewardRedeemed.Redemption.Reward.Id]);
                }

                //TODO sistema de verdade
                if(e.RewardRedeemed.Redemption.Reward.Title == "Nao pode pular")
                {
                    External.SendToOverlay(Encoding.UTF8.GetBytes(@"
                        {""event_name"": ""jumpTimer"", ""label"": ""Não pode pular!"", ""seconds"": 5}
                    "));
                }
            }

            if (e.RewardRedeemed.Redemption.Reward.Title == "Deletar coisas")
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
