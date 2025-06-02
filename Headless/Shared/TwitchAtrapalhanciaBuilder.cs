using System.Reflection;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace Shared
{
    public class TwitchAtrapalhanciaBuilder
    {
        private Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
        {
            var assembly = ((AppDomain)sender).GetAssemblies().FirstOrDefault(x => x.FullName == args.Name);
            return assembly;
        }

        public Dictionary<string, CreateCustomRewardsRequest> BuildRewardsFromFile(string assemblyPath)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            Assembly.Load(File.ReadAllBytes(Environment.CurrentDirectory + $"/Atrapalhancias/AtrapalhanciasBase.dll"));
            var atrapalhanciaAssembly = Assembly.Load(File.ReadAllBytes(assemblyPath));

            var rewards = new Dictionary<string, CreateCustomRewardsRequest>();

            var atrapalhancias = atrapalhanciaAssembly.ExportedTypes.First(x => x.BaseType.Name.ToUpper() == "BASEATRAPALHANCIAGAME");
            foreach (MethodInfo mi in atrapalhancias.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(p => p.ReturnType == typeof(Task)))
            {
                rewards.Add(mi.Name.ToLower(), new CreateCustomRewardsRequest { Title = mi.CustomAttributes.First().ConstructorArguments[0].Value.ToString(), Cost = Convert.ToInt32(mi.CustomAttributes.First().ConstructorArguments[1].Value), BackgroundColor = "#2C2C2C", IsEnabled = true });
            }

            return rewards;
        }
    }
}
