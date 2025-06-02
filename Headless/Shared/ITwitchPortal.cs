using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;

namespace Shared
{
    public interface ITwitchPortal
    {
        void CreateRedeemRewards(Dictionary<string, CreateCustomRewardsRequest> atrapalhanciaRewards);
        void SendChatMessage(string message);
    }
}
