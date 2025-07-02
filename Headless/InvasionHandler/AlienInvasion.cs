using Headless.Shared;
using Newtonsoft.Json;
using System.Text;

namespace InvasionHandler //TODO decouple this to shared so that I can import it on the twitch handler?
{
    public class AlienInvasion
    {
        private int AlienFury = 0;
        private int ReneChaosCounter = 0;

        public void ReneMentioned()
        {
            ReneChaosCounter++;
            if (ReneChaosCounter == 3)
            {
                //if (AlienFury == 2)
                //{
                    //External.SendToOverlay["umjotas"](Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                    //{
                    //    event_name = "invasion"
                    //})));
                    //Console.WriteLine("Alien fury unleashed!");
                    //AlienFury = 0;
                //}
               //else
               //{
               //
               //    AlienFury++;
               //}
                ReneChaosCounter = 0;
            }
            else
            {
                //External.SendToOverlay["umjotas"](Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                //{
                //    event_name = "tchans"
                //})));
            }
        }
    }
}
