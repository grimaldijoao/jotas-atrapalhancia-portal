using Headless.Shared.Interfaces;

namespace Headless.Shared
{
    public class User : IAtrapalhator, IUser
    {
        private string username;
        public string UserName => username;

        private string profilePic;


        public User(string username, string profilePic)
        {
            this.username = username;
            this.profilePic = profilePic;
        }

        //? Uma vez alguem no chat falou q a atrapalhancia tava "MTO INSTA", quanto mais coisinhas tiver pra mandar e receber uma atrapalhancia, mais lag, pq esses são os ms que importam, era pra ser no maximo o ms do websocket e fim.
        public void Atrapalhate(string channel, string atrapalhancia)
        {
            External.SendToBroadcaster[channel]($"atrapalhancia/{atrapalhancia}/{username}/{profilePic.Replace("/", "%2F")}");
        }
    }
}
