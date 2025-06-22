namespace AtrapalhanciaWebSocket
{
    public abstract class BaseServiceBehavior
    {
        public string Route { get; private set; }

        internal EventHandler<string> OnMessageEvent;
        internal EventHandler OnOpenEvent;
        internal EventHandler OnCloseEvent;

        internal Func<string, Task> send;
        internal Action close;

        public BaseServiceBehavior(string route)
        {
            Route = route;
            OnOpenEvent += (_, _) => OnOpen();
            OnCloseEvent += (_, _) => OnClose();
            OnMessageEvent += (_, message) => OnMessage(message);
        }

        public async void SendAsync(string message)
        {
            await send(message);
        }

        public void Close()
        {
            close();
        }

        protected abstract void OnMessage(string message);

        protected abstract void OnOpen();

        protected abstract void OnClose();
    }
}
