using Headless.Shared;
using InvasionHandler;
using JotasTwitchPortal.JSON;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using TwitchLib.Api;
using TwitchLib.Api.Core.Exceptions;
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
        private string BroadcasterId;
        private string AccessToken;

        string clientId = "fzrpx1kxpqk3cyklu4uhw9q0mpux2y";
        string bot_access_token = File.ReadAllText("bot_access_token.txt");
        TwitchClient client;
        TwitchAPI _api;
        TwitchAPI api
        {
            get
            {
                return _api;
            }
            set
            {
                if(value == null)
                {
                    Console.WriteLine($"wtf null api {ChannelName} {BroadcasterId}");
                }
                _api = value;
            }
        }
        EventSub eventSub;

        private Dictionary<string, CreateCustomRewardsRequest> CurrentRewards = new Dictionary<string, CreateCustomRewardsRequest>();

        private AlienInvasion Invasion = new AlienInvasion(); //TODO generic<T> module adder instead of everyone having this? lol

        private Dictionary<string, User> ConnectedUsers = new Dictionary<string, User>();

        public Twitch(string broadcaster_id, string channel, string accessToken)
        {
            ChannelName = channel;
            BroadcasterId = broadcaster_id;
            AccessToken = accessToken;
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

            api.Settings.AccessToken = AccessToken;
            api.Settings.ClientId = clientId;

            var tokenValidation = api.Auth.ValidateAccessTokenAsync(AccessToken).GetAwaiter().GetResult();

            if(tokenValidation == null)
            {
                throw new BadRequestException($"Invalid token for {ChannelName}");
            }

            client.OnLog += Client_OnLog;
            client.OnUserJoined += Client_OnUserJoined;
            client.OnJoinedChannel += Client_OnJoinedChannel;
            client.OnMessageReceived += Client_OnMessageReceived;


            client.Connect();


            eventSub = new EventSub();
            eventSub.OnChatRewardRedeemed += EventSub_OnChatRewardRedeemed;
            eventSub.OnConnected += EventSub_OnConnected;
            eventSub.Connect();


            Console.WriteLine($"{ChannelName} connected!");
        }

        private void EventSub_OnConnected(object sender, string sessionId)
        {
            var result = api.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_custom_reward_redemption.add", "1", new Dictionary<string, string>() { { "broadcaster_user_id", BroadcasterId } }, TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket, sessionId, null, null, clientId, AccessToken).GetAwaiter().GetResult();
        }

        public void SendChatMessage(string message, TwitchClient client)
        {
            client.SendMessage(client.JoinedChannels.ElementAt(0), message); //? maybe i should use a single client for everyone...
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

        public async Task<bool> DeleteRedeemReward(string rewardId)
        {
            //TODO if user doesnt connect (frontend fails) handle this gracefully
            try
            {
                await api.Helix.ChannelPoints.DeleteCustomRewardAsync(BroadcasterId, rewardId);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete reward {rewardId}: {ex.Message}");
                return false;
            }
        }

        public void DeleteRedeemRewards()
        {
            var tasks = new List<Task>();

            foreach (var rewardId in CurrentRewards.Keys)
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await api.Helix.ChannelPoints.DeleteCustomRewardAsync(BroadcasterId, rewardId);
                    }
                    catch (BadRequestException ex)
                    {
                        Console.WriteLine($"Failed to delete reward {rewardId}: {ex.Message}");
                    }
                });

                tasks.Add(task);
            }

            Task.WhenAll(tasks).GetAwaiter().GetResult();
        }

        public async Task<CreateCustomRewardsResponse[]> CreateRedeemRewardsAsync(Dictionary<string, CreateCustomRewardsRequest> atrapalhanciaRewards)
        {
            var creationResults = new List<CreateCustomRewardsResponse>();
            var cts = new CancellationTokenSource();
            var tasks = new List<Task>();

            var lockObj = new object();

            foreach (var reward in atrapalhanciaRewards)
            {
                var key = reward.Key;
                var request = reward.Value;

                var task = Task.Run(async () =>
                {
                    try
                    {
                        cts.Token.ThrowIfCancellationRequested();

                        var customReward = await api.Helix.ChannelPoints.CreateCustomRewardsAsync(BroadcasterId, request, AccessToken);
                        var rewardId = customReward.Data.First().Id;

                        TwitchRewardsAtrapalhancias[rewardId] = key;

                        lock (lockObj)
                        {
                            creationResults.Add(customReward);
                            CurrentRewards.Add(rewardId, reward.Value);
                        }
                    }
                    catch (BadRequestException ex)
                    {
                        Console.WriteLine($"Failed to create reward '{key}': {ex.Message}");
                        cts.Cancel();
                        throw;
                    }
                }, cts.Token);

                tasks.Add(task);
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch
            {
                throw;
            }

            return creationResults.ToArray();
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
                    //External.SendToOverlay[ChannelName](Encoding.UTF8.GetBytes(@"
                    //    {""event_name"": ""jumpTimer"", ""label"": ""Não pode pular!"", ""seconds"": 5}
                    //"));
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

            SendChatMessage("🤖🤝👽", (TwitchClient)sender);
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
            DeleteRedeemRewards();

            api = null;

            client.Disconnect();
            client = null;
            eventSub.Dispose();
        }
    }
}
