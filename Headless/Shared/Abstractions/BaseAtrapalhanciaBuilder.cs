using System.Reflection;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace Headless.Shared.Abstractions
{
    public abstract class BaseAtrapalhanciaBuilder<T>
    {
        private static Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
        {
            var assembly = ((AppDomain)sender).GetAssemblies().FirstOrDefault(x => x.FullName == args.Name);
            return assembly;
        }

        protected virtual IEnumerable<MethodInfo> ProcessAssembly(string assemblyPath)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            Assembly.Load(File.ReadAllBytes(Environment.CurrentDirectory + $"/Atrapalhancias/AtrapalhanciasBase.dll"));
            var atrapalhanciaAssembly = Assembly.Load(File.ReadAllBytes(assemblyPath));

            var rewards = new Dictionary<string, CreateCustomRewardsRequest>();

            var atrapalhancias = atrapalhanciaAssembly.ExportedTypes.First(x => x.BaseType.Name.ToUpper() == "BASEATRAPALHANCIAGAME");

            return atrapalhancias.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(p => p.ReturnType == typeof(Task));
        }

        protected abstract T ParseAtrapalhanciaAssemblies(IEnumerable<MethodInfo> taskMethodInfos);

        public T BuildAtrapalhanciasFromFile(string assemblyPath)
        {
            var taskMethodInfos = ProcessAssembly(assemblyPath);
            return ParseAtrapalhanciaAssemblies(taskMethodInfos);
        }
    }
}
