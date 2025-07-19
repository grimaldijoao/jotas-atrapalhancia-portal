using AtrapalhanciaDatabase.Tables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Utils;
using System.Net;
using System.Text;

namespace TwitchHandler
{
    public class EventSub
    {
        private const string callbackUrl = "https://api.atrapalhancias.com.br/twitch-reward-eventsub/";

        private string clientId;
        private string clientSecret;
        private string webHookSecret;
        private string broadcasterId;
        private string channelName;

        private string? subscriptionId;

        public EventSub(string clientId, string clientSecret, string webHookSecret, string broadcasterId, string channelName)
        {
            this.clientId = clientId;
            this.clientSecret = clientSecret;
            this.webHookSecret = webHookSecret;
            this.broadcasterId = broadcasterId;
            this.channelName = channelName;

            subscriptionId = TwitchRelation.GetSubscriptionId(broadcasterId);
        }

        public string? GetAppToken()
        {
            var client = new HttpClient();

            var request = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token");

            var collection = new List<KeyValuePair<string, string>>();

            collection.Add(new("client_id", clientId));
            collection.Add(new("client_secret", clientSecret));
            collection.Add(new("grant_type", "client_credentials"));

            object content = new FormUrlEncodedContent(collection);
            request.Content = (FormUrlEncodedContent)content;

            var response = client.SendAsync(request).GetAwaiter().GetResult();
            var result = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();

            return JsonConvert.DeserializeObject<JObject>(result)?.GetValue("access_token")?.Value<string>();
        }

        public void RegisterWithCleanup()
        {
            var appToken = GetAppToken();

            if (appToken == null)
            {
                throw new ArgumentNullException("Failed to get app token");
            }

            var client = new HttpClient();

            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/eventsub/subscriptions");
            request.Headers.Add("Authorization", $"Bearer {appToken}");
            request.Headers.Add("Client-Id", clientId);

            var response = client.SendAsync(request).GetAwaiter().GetResult();

            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            var obj = JObject.Parse(json);
            var match = obj["data"]?
                .FirstOrDefault(d => (string?)d["transport"]?["callback"] == callbackUrl);

            string? id = (string?)match?["id"];

            if(id != null)
            {
                client = new HttpClient();
                
                request = new HttpRequestMessage(HttpMethod.Delete, $"https://api.twitch.tv/helix/eventsub/subscriptions?id={id}");
                request.Headers.Add("Authorization", $"Bearer {appToken}");
                request.Headers.Add("Client-Id", clientId);

                response = client.SendAsync(request).GetAwaiter().GetResult();
                response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if(response.StatusCode != HttpStatusCode.NoContent)
                {
                    TimestampedConsole.Log($"Tried to clean {channelName} subscription but DELETE failed");
                }
                else
                {
                    RegisterChannel();
                }

            }
            else
            {
                TimestampedConsole.Log($"Tried to clean {channelName} subscription but found no matching id");
            }

        }

        public void RegisterChannel()
        {
            if(subscriptionId != null)
            {
                RemoveChannel();
            }

            var appToken = GetAppToken();

            if (appToken == null)
            {
                throw new ArgumentNullException("Failed to get app token");
            }

            var client = new HttpClient();

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitch.tv/helix/eventsub/subscriptions");
            request.Headers.Add("Client-Id", clientId);
            request.Headers.Add("Authorization", $"Bearer {appToken}");
            var content = JsonConvert.SerializeObject(new
            {
                type = "channel.channel_points_custom_reward_redemption.add",
                version = "1",
                condition = new { broadcaster_user_id = broadcasterId },
                transport = new
                {
                    method = "webhook",
                    callback = callbackUrl,
                    secret = webHookSecret
                }
            });
            request.Content = new StringContent((string)content, Encoding.UTF8, "application/json");

            var response = client.SendAsync(request).GetAwaiter().GetResult();
            
            if(response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new HttpRequestException("Subscription for this channel and endpoint already exists");
            }
            
            response.EnsureSuccessStatusCode();

            var result = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();

            TimestampedConsole.Log(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

            var json = JObject.Parse(result);
            string? responseSubscriptionId = json["data"]?[0]?["id"]?.ToString();

            if (responseSubscriptionId == null)
            {
                throw new ArgumentNullException("Failed to create subscription (or get subscription id)");
            }

            subscriptionId = responseSubscriptionId;

            TwitchRelation.UpdateSubscriptionId(broadcasterId, subscriptionId);

            Console.WriteLine($"Registered {channelName} eventsub");
        }

        public void RemoveChannel()
        {
            if(subscriptionId != null)
            {
                Console.WriteLine($"Removing {channelName} eventsub");
            }
            else
            {
                Console.WriteLine($"Trying to remove subscription that has not been registered for {channelName}!");
            }
        }
    }   
}
