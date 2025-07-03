using JotasTwitchPortal.JSON;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace TwitchHandler
{

    public class TwitchMessage
    {
        public class Metadata
        {
            public string message_id { get; set;}
            public string message_type { get; set; }
            public string message_timestamp { get; set;}
            public string subscription_type { get; set; }
        }

        public Metadata metadata { get; set;}
        public JObject payload { get; set;}
    }

    public static class EventSub
    {
        private static ConcurrentDictionary<string, Action<RewardEvent>> ChatRewardRedeemEvents = new ConcurrentDictionary<string, Action<RewardEvent>>();
        static ClientWebSocket ws = new ClientWebSocket();

        public static string? SessionId { get; private set; }
        public static bool Connected { get; private set; }

        private static CancellationTokenSource ReconnectToken = new CancellationTokenSource();

        public static void RegisterChannel(string channelName, Action<RewardEvent> OnChatRewardRedeemed)
        {
            ChatRewardRedeemEvents[channelName] = OnChatRewardRedeemed;
        }

        public static void RemoveChannel(string channelName)
        {
            ChatRewardRedeemEvents.Remove(channelName, out _);
        }

        public static void StaleReconnectionToken()
        {
            ReconnectToken.Cancel();
        }

        public static async Task ConnectWithRetryAsync()
        {
            while (true)
            {
                using (ws = new ClientWebSocket())
                {
                    try
                    {
                        Console.WriteLine("Connecting to EventSub...");
                        await ws.ConnectAsync(new Uri("wss://eventsub.wss.twitch.tv/ws"), CancellationToken.None);

                        Connected = true;

                        var buffer = new byte[4096];

                        while (ws.State == WebSocketState.Open)
                        {
                            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                var message = JsonConvert.DeserializeObject<TwitchMessage>(json);
                                if (message?.metadata?.message_type == "session_welcome")
                                {
                                    SessionId = message.payload["session"]?.Value<string>("id");
                                    Console.WriteLine("EventSub connected with session ID: " + SessionId);
                                }
                                else if (message.metadata.message_type == "notification" &&
                                         message.metadata.subscription_type == "channel.channel_points_custom_reward_redemption.add")
                                {
                                    var redeem = JsonConvert.DeserializeObject<RewardEvent>(message.payload.ToString());
                                    if (redeem != null && ChatRewardRedeemEvents.TryGetValue(redeem.Event.BroadcasterUserLogin, out var eventCaller))
                                    {
                                        eventCaller(redeem);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("EventSub connection error: " + ex.Message);
                    }

                    Connected = false;
                    SessionId = null;
                    Console.WriteLine("EventSub connection lost. Waiting for reconnection");
                }

                var localToken = ReconnectToken.Token;
                await Task.Delay(60000, localToken);

                ReconnectToken = new CancellationTokenSource();
            }
        }
    }
}
