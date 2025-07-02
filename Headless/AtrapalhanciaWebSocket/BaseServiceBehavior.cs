namespace AtrapalhanciaWebSocket
{
    public abstract class BaseServiceBehavior : IServiceBehaviorEventsInvoker
    {
        public string Route { get; private set; }

        event EventHandler<string> OnMessageEvent;
        event EventHandler OnOpenEvent;
        event EventHandler OnCloseEvent;

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

        void IServiceBehaviorEventsInvoker.InvokeOpenEvent()
        {
            OnOpenEvent.Invoke(this, EventArgs.Empty);
        }

        void IServiceBehaviorEventsInvoker.InvokeMessageEvent(string message)
        {
            OnMessageEvent.Invoke(this, message);
        }

        void IServiceBehaviorEventsInvoker.InvokeCloseEvent()
        {
            OnCloseEvent.Invoke(this, EventArgs.Empty);
        }
    }
}
