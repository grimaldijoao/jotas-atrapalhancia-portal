using HeadlessAtrapalhanciaHandler;
using TwitchLib.Api.Helix.Models.Games;

namespace JotasTwitchPortal
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var channelName = "umjotas";
            var socketManager = new WebSocketManager();

            var bot = new Bot(ref socketManager.server, channelName);
            bot.Connect();

            socketManager.Initialize(channelName, (sender, gameName) => //TODO race condition?
            {
                GameService service = sender;
                External.BroadcasterSocket = service.Context.WebSocket;

                var rewards = new TwitchAtrapalhanciaBuilder().BuildRewardsFromFile(Environment.CurrentDirectory + $"/Atrapalhancias/{gameName}.dll");
                bot.CreateRedeemRewards(rewards);
            });



            HttpServer.Run(ref socketManager.server);

            Console.WriteLine("🧙 Portal aberto! 🧙");
            Console.ReadLine();

        }
    }
}
