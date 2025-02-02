namespace JotasTwitchPortal
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var portal = new Portal();
            var socketServer = portal.Run();

            var bot = new Bot(socketServer);
            bot.Connect();

            Console.WriteLine("🧙 Portal aberto! 🧙");
            Console.ReadLine();


        }
    }
}
