using HeadlessAtrapalhanciaHandler;

namespace JotasTwitchPortal
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var socketManager = new WebSocketManager();
            socketManager.Initialize();

            var bot = new Bot(ref socketManager.server);
            bot.Connect();

            HttpServer.Run(ref socketManager.server);

            Console.WriteLine("🧙 Portal aberto! 🧙");
            Console.ReadLine();

        }
    }
}
