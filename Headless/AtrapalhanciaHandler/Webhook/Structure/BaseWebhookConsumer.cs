using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;

namespace AtrapalhanciaHandler.Webhook.Structure
{
    public abstract class BaseWebhookConsumer<T> where T : class
    {
        public event Action<T> OnWebhookRequest;

        protected abstract T ProcessRequest(string bodyAsString, Encoding encoding);
        protected abstract string PrepareMessageForHmac(NameValueCollection headers, string body);
        protected abstract string GetIncomingSignature(NameValueCollection headers);
        protected abstract string GetSecret(NameValueCollection headers);

        private string GetHmac(NameValueCollection headers, string body)
        {
            var message = PrepareMessageForHmac(headers, body);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(GetSecret(headers)));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return $"sha256={BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()}";
        }

        private bool VerifyMessage(string hmac, string incomingSignature)
        {
            var hmacBytes = Encoding.UTF8.GetBytes(hmac);
            var signatureBytes = Encoding.UTF8.GetBytes(incomingSignature);
            return CryptographicOperations.FixedTimeEquals(hmacBytes, signatureBytes);
        }    
        
        public T? HandleRequest(NameValueCollection headers, Stream bodyStream, Encoding encoding)
        {
            string bodyAsString;
            using (var reader = new StreamReader(bodyStream, encoding))
            {
                bodyAsString = reader.ReadToEnd();
            }

            T body = ProcessRequest(bodyAsString, encoding);

            if (VerifyMessage(GetHmac(headers, bodyAsString), GetIncomingSignature(headers)))
            {
                return body;
            }

            return null;
        }
    }
}
