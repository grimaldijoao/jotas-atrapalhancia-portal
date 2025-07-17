using AtrapalhanciaHandler.Webhook.Structure;
using Newtonsoft.Json;
using Shared.JSON;
using System.Collections.Specialized;
using System.Text;

namespace AtrapalhanciaHandler.Webhook
{
    [WebhookAttribute("/twitch-reward-eventsub")]
    public class TwitchWebhookConsumer : BaseWebhookConsumer<TwitchRewardPayload>
    {
        private string webhookSecret;

        public TwitchWebhookConsumer(string secret)
        {
            webhookSecret = secret;
        }

        protected override string GetIncomingSignature(NameValueCollection headers)
        {
            return headers["Twitch-Eventsub-Message-Signature".ToLower()];
        }

        protected override string GetSecret(NameValueCollection headers)
        {
            return webhookSecret;
        }

        protected override string PrepareMessageForHmac(NameValueCollection headers, string body)
        {
            var id = headers["Twitch-Eventsub-Message-Id".ToLower()];
            var timestamp = headers["Twitch-Eventsub-Message-Timestamp".ToLower()];

            return id + timestamp + body;
        }

        protected override TwitchRewardPayload ProcessRequest(string bodyAsString, Encoding encoding)
        {
            return JsonConvert.DeserializeObject<TwitchRewardPayload>(bodyAsString)!;
        }
    }
}
