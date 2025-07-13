using Headless.Shared.Interfaces;

namespace Headless.Shared
{
    public class User : IAtrapalhator, IUser
    {
        public string UserName { get; private set; }
        public string ProfilePic { get; private set; }

        private Func<string, string, Task> internal_atrapalhate = (user, msg) => {
            Console.WriteLine($"{user} tried to atrapalhate without internal function ({msg})");
            return Task.CompletedTask;
        };

        public User(string username, string profilePic)
        {
            this.UserName = username;
            this.ProfilePic = profilePic;
        }

        //? Uma vez alguem no chat falou q a atrapalhancia tava "MTO INSTA", quanto mais coisinhas tiver pra mandar e receber uma atrapalhancia, mais lag, pq esses são os ms que importam, era pra ser no maximo o ms do websocket e fim.
        public void Atrapalhate(string channel, string atrapalhancia)
        {
            Console.WriteLine($"{UserName} sending {atrapalhancia}");
            internal_atrapalhate(UserName, $"atrapalhancia/{atrapalhancia}/{UserName}/{ProfilePic.Replace("/", "%2F")}");
        }

        public void LoadAtrapalhanciaSender(Func<string, Task> atrapalhate) //TODO base class thing where this gets overwritted and the actual Atrapalhate is not overwrittable and runs this
        {
            internal_atrapalhate = (_, msg) => atrapalhate(msg);
        }
    }
}
