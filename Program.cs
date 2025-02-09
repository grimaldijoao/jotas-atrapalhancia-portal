using HeadlessAtrapalhanciaHandler;
using System.Reflection;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace JotasTwitchPortal
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var portal = new Portal();
            var socketServer = portal.Run();

            WebSocket.OnConnected += WebSocket_OnConnected;

            var bot = new Bot(socketServer);
            bot.Connect();

            HttpServer.Run(socketServer);

            Console.WriteLine("🧙 Portal aberto! 🧙");
            Console.ReadLine();

        }

        private static void WebSocket_OnConnected(object? sender, string gameName)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            Assembly.Load(File.ReadAllBytes(Environment.CurrentDirectory + $"/Atrapalhancias/AtrapalhanciasBase.dll"));
            var atrapalhanciaAssembly = Assembly.Load(File.ReadAllBytes(Environment.CurrentDirectory + $"/Atrapalhancias/{gameName}.dll"));

            var rewards = new Dictionary<string, CreateCustomRewardsRequest>();

            var atrapalhancias = atrapalhanciaAssembly.ExportedTypes.First(x => x.BaseType.Name.ToUpper() == "BASEATRAPALHANCIAGAME");
            foreach (MethodInfo mi in atrapalhancias.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(p => p.ReturnType == typeof(Task)))
            {
                rewards.Add(mi.Name.ToLower(), new CreateCustomRewardsRequest { Title = mi.CustomAttributes.First().ConstructorArguments[0].Value.ToString(), Cost = Convert.ToInt32(mi.CustomAttributes.First().ConstructorArguments[1].Value), BackgroundColor = "#2C2C2C", IsEnabled = true });
            }

            Bot.CurrentBot.CreateRedeemRewards(rewards);
        }

        private static Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
        {
            var assembly = ((AppDomain)sender).GetAssemblies().FirstOrDefault(x => x.FullName == args.Name);
            return assembly;
        }
    }
}
