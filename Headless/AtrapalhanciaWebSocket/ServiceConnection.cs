using System.Net.Sockets;

namespace AtrapalhanciaWebSocket
{
    public class ServiceConnection<T> where T : BaseServiceBehavior
    {
        public TcpClient TcpClient { get; private set; }
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
        public T Behavior { get; private set; }

        internal ServiceConnection(TcpClient client, T behavior)
        {
            TcpClient = client;
            Behavior = behavior;
        }
    }

    public static class ServiceConnection
    {
        public static ServiceConnection<T> Create<T>(TcpClient client, T behavior)
            where T : BaseServiceBehavior
        {
            return new ServiceConnection<T>(client, behavior);
        }
    }
}
