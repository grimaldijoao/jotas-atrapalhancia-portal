using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Resources;
using System.Text;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.PubSub;
using WebSocketSharp.Server;

namespace JotasTwitchPortal
{
    public class Bot
    {
        string bot_access_token = File.ReadAllText("bot_access_token.txt");
        string access_token = File.ReadAllText("access_token.txt");
        TwitchClient client;
        TwitchPubSub clientPubSub;
        TwitchAPI api;

        string broadcasterId;
        string moderatorId;

        private WebSocketServiceManager socketServer;

        public Bot(WebSocketServiceManager _socketServer)
        {
            socketServer = _socketServer;
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
            client.Initialize(credentials, "umjotas");

            api = new TwitchAPI();
            api.Settings.AccessToken = bot_access_token;
            api.Settings.ClientId = "gp762nuuoqcoxypju8c569th9wz7q5";

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

        private void ClientPubSub_OnPubSubServiceConnected(object sender, EventArgs e)
        {
            clientPubSub.ListenToChannelPoints("235332563");
            clientPubSub.SendTopics(access_token);
            Console.WriteLine("Pubsub Abrido!");
        }

        private void ClientPubSub_OnChatRewardRedeemed(object? sender, TwitchLib.PubSub.Events.OnChannelPointsRewardRedeemedArgs e)
        {
            if(e.RewardRedeemed.Redemption.Reward.Title == "Deletar coisas")
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
            socketServer.Broadcast(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
            {
                username = "umjotas",
                profile_pic = "https://static-cdn.jtvnw.net/jtv_user_pictures/f2ce0467-b3d6-4780-927a-8c38cd0bed0f-profile_image-70x70.png",
                event_name = "porta"
            })));
            Console.WriteLine($"Connected to {e.AutoJoinChannel}");
        }

        HashSet<string> UniqueUsers = new HashSet<string>();
        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            var userData = api.Helix.Users.GetUsersAsync(logins: new List<string>() { "umjotas", "umbotas" }).GetAwaiter().GetResult().Users;
            broadcasterId = userData[0].Id;
            moderatorId = userData[1].Id;

            client.SendMessage("umjotas", "🤖🤝👽");
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            if (UniqueUsers.Add(e.ChatMessage.Username))
            {
                var userData = api.Helix.Users.GetUsersAsync(logins: new List<string>() { e.ChatMessage.Username }).GetAwaiter().GetResult().Users[0];
                Console.WriteLine(e.ChatMessage.Username, "portou");
                socketServer.Broadcast(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                {
                    username = userData.DisplayName,
                    profile_pic = userData.ProfileImageUrl,
                    event_name = "porta"
                })));
            }

            if (e.ChatMessage.Message.Contains("rene"))
                api.Helix.Moderation.DeleteBlockedTermAsync(broadcasterId, moderatorId, "rene");
        }

        private void Client_OnUserJoined(object sender, OnUserJoinedArgs e)
        {
            if (UniqueUsers.Add(e.Username))
            {
                var userData = api.Helix.Users.GetUsersAsync(logins: new List<string>() { e.Username }).GetAwaiter().GetResult().Users[0];
                Console.WriteLine(e.Username, "portou");
                socketServer.Broadcast(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                {
                    username = userData.DisplayName,
                    profile_pic = userData.ProfileImageUrl,
                    event_name = "porta"
                })));
            }
        }

        private void Client_OnWhisperReceived(object sender, OnWhisperReceivedArgs e)
        {
            if (e.WhisperMessage.Username == "my_friend")
                client.SendWhisper(e.WhisperMessage.Username, "Hey! Whispers are so cool!!");
        }

        private void Client_OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            if (e.Subscriber.SubscriptionPlan == SubscriptionPlan.Prime)
                client.SendMessage(e.Channel, $"Welcome {e.Subscriber.DisplayName} to the substers! You just earned 500 points! So kind of you to use your Twitch Prime on this channel!");
            else
                client.SendMessage(e.Channel, $"Welcome {e.Subscriber.DisplayName} to the substers! You just earned 500 points!");
        }
    }
}
