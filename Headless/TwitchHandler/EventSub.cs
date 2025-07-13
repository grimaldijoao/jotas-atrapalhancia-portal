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
            public string message_id { get; set; }
            public string message_type { get; set; }
            public string message_timestamp { get; set; }
            public string subscription_type { get; set; }
        }

        public Metadata metadata { get; set; }
        public JObject payload { get; set; }
    }

    public static class EventSub
    {
        private static readonly ConcurrentDictionary<string, Action<RewardEvent>> chatRewardRedeemEvents = new ConcurrentDictionary<string, Action<RewardEvent>>();

        private static readonly object connectionAttemptLock = new object();
        private static Task? connectionLoop;

        private static TaskCompletionSource<bool> connectionEstablishCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public static string? SessionId { get; private set; }

        public static void AttemptConnection()
        {
            lock (connectionAttemptLock)
            {
                if (connectionLoop == null || connectionLoop.IsCompleted)
                {
                    connectionLoop = Task.Run(() =>
                    {
                        return EstablishConnectionAsync();
                    });
                }
            }
        }

        public static Task WaitForConnectionAsync()
        {
            return connectionEstablishCompleted.Task;
        }

        public static void RegisterChannel(string channelName, Action<RewardEvent> onChatRewardRedeemed)
        {
            chatRewardRedeemEvents[channelName] = onChatRewardRedeemed;
            Console.WriteLine($"Registered {channelName} eventsub");
        }

        public static void RemoveChannel(string channelName)
        {
            chatRewardRedeemEvents.TryRemove(channelName, out _);
            Console.WriteLine($"Removing {channelName} eventsub");
        }

        private static async Task EstablishConnectionAsync()
        {
            using (var webSocketClient = new ClientWebSocket())
            {
                try
                {
                    Console.WriteLine("Connecting to EventSub...");
                    await webSocketClient.ConnectAsync(new Uri("wss://eventsub.wss.twitch.tv/ws"), CancellationToken.None);

                    var buffer = new byte[4096];
                    while (webSocketClient.State == WebSocketState.Open)
                    {
                        var result = await webSocketClient.ReceiveAsync(buffer, CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await webSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        }
                        else
                        {
                            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            if (string.IsNullOrWhiteSpace(json)) continue;

                            var message = JsonConvert.DeserializeObject<TwitchMessage>(json);
                            if (message?.metadata?.message_type == "session_welcome")
                            {
                                SessionId = message.payload["session"]?.Value<string>("id");
                                Console.WriteLine("EventSub connected with session ID: " + SessionId);
                                connectionEstablishCompleted.TrySetResult(true);
                            }
                            else if (message.metadata.message_type == "notification" &&
                                     message.metadata.subscription_type == "channel.channel_points_custom_reward_redemption.add")
                            {
                                var redeem = JsonConvert.DeserializeObject<RewardEvent>(message.payload.ToString());
                                if (redeem != null && chatRewardRedeemEvents.TryGetValue(redeem.Event.BroadcasterUserLogin, out var eventCaller))
                                {
                                    Console.WriteLine($"Mandando redeem pro {redeem.Event.BroadcasterUserLogin} - {redeem.Event.Reward.Title}");
                                    eventCaller(redeem);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"EventSub connection error: {ex.Message}");
                }
                finally
                {
                    SessionId = null;
                    Console.WriteLine("EventSub connection lost. Waiting for reconnection...");
                    connectionEstablishCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    //? Invoke some event to warn the consumer that the global websocket connection was lost, so that it can (optionally) re-try to connect (and then trigger the eventsub http subscribe request)
                }
            }
        }
    }   
}
