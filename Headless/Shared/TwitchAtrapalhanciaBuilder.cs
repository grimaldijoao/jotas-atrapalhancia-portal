using Headless.Shared.Abstractions;
using System.Reflection;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace Headless.Shared
{
    public class TwitchAtrapalhanciaBuilder : BaseAtrapalhanciaBuilder<Dictionary<string, CreateCustomRewardsRequest>>
    {
        protected override Dictionary<string, CreateCustomRewardsRequest> ParseAtrapalhanciaAssemblies(IEnumerable<MethodInfo> taskMethodInfos)
        {
            var rewards = new Dictionary<string, CreateCustomRewardsRequest>();

            foreach (MethodInfo mi in taskMethodInfos)
            {
                rewards.Add(mi.Name.ToLower(), new CreateCustomRewardsRequest {
                    Title = mi.CustomAttributes.First().ConstructorArguments[0].Value.ToString(),
                    Cost = Convert.ToInt32(mi.CustomAttributes.First().ConstructorArguments[1].Value),
                    BackgroundColor = "#0AE6BB",
                    IsEnabled = true
                });
            }

            return rewards;
        }
    }
}
