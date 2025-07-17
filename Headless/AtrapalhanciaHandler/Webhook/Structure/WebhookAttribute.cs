namespace AtrapalhanciaHandler.Webhook.Structure
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class WebhookAttribute : Attribute
    {
        private string Route;

        public WebhookAttribute(string route)
        {
            Route = route;
        }
    }
}
