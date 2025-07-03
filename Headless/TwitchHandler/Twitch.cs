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
        public string ChannelName { get; private set; }
        private string BroadcasterId;
        private string AccessToken;

        string clientId = "fzrpx1kxpqk3cyklu4uhw9q0mpux2y";
        string bot_access_token = File.ReadAllText("bot_access_token.txt");
        string? rewardSubscriptionId;

        TwitchClient client;
        TwitchAPI api;

        private Dictionary<string, CreateCustomRewardsRequest> CurrentRewards = new Dictionary<string, CreateCustomRewardsRequest>();

        private Dictionary<string, User> ConnectedUsers = new Dictionary<string, User>();

        public event EventHandler<User> OnAtrapalhanciaUserCreated;

        public Twitch(string broadcaster_id, string channel, string accessToken)
        {
            ChannelName = channel;
            BroadcasterId = broadcaster_id;
            AccessToken = accessToken;
        }

        public void Connect()
        {
            if(EventSub.Connected)
            {
                EventSub.StaleReconnectionToken();
            }
            else
            {
                EventSub.ConnectWithRetryAsync();
            }

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

            try
            {
                var result = api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.channel_points_custom_reward_redemption.add",
                    "1",
                    new Dictionary<string, string>() { { "broadcaster_user_id", BroadcasterId } },
                    TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                    websocketSessionId: EventSub.SessionId,
                    clientId: clientId,
                    accessToken: AccessToken
                ).GetAwaiter().GetResult();

                rewardSubscriptionId = result.Subscriptions.Last().Id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Exception during EventSub subscription: {ex.Message}");
            }

            EventSub.RegisterChannel(ChannelName, EventSub_OnChatRewardRedeemed);

            Console.WriteLine($"{ChannelName} connected!");
        }

        private void EventSub_OnChatRewardRedeemed(RewardEvent rewardEvent)
        {
            string atrapalhancia;
            User user;

            if (TwitchRewardsAtrapalhancias.TryGetValue(rewardEvent.Event.Reward.Id, out atrapalhancia))
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
            }
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

            var user = new User(username, userData.ProfileImageUrl);

            OnAtrapalhanciaUserCreated.Invoke(this, user);

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
                        Console.WriteLine($"Actually removing {CurrentRewards[rewardId].Title} on twitch for {ChannelName}");
                        await api.Helix.ChannelPoints.DeleteCustomRewardAsync(BroadcasterId, rewardId);
                    }
                    catch (BadRequestException ex)
                    {
                        Console.WriteLine($"Failed to delete {ChannelName} reward {rewardId}: {ex.Message}");
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

        private void Client_OnLog(object sender, OnLogArgs e)
        {
            Console.WriteLine($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            var userData = api.Helix.Users.GetUsersAsync(logins: new List<string>() { e.Channel, "umbotas" }).GetAwaiter().GetResult().Users;
            
            Console.WriteLine($"Connected to twitch channel {e.Channel}!");

            SendChatMessage("🤖🤝👽", (TwitchClient)sender);
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            TryFirstJoin(e.ChatMessage.Username);
            if(e.ChatMessage.Message == "jump")
            {
                ConnectedUsers[e.ChatMessage.Username].Atrapalhate(e.ChatMessage.Channel, "Jump");
            }
        }

        private void Client_OnUserJoined(object sender, OnUserJoinedArgs e)
        {
            TryFirstJoin(e.Username);
        }

        public void Dispose()
        {
            EventSub.RemoveChannel(ChannelName);
            
            if(rewardSubscriptionId != null)
            {
                api.Helix.EventSub.DeleteEventSubSubscriptionAsync(rewardSubscriptionId, clientId, AccessToken);
            }

            DeleteRedeemRewards();

            api = null;

            client.Disconnect();
            client = null;
        }
    }
}
