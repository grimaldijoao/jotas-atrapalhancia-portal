namespace Headless.Shared
{
    public class External
    {
        public static Action<byte[]> SendToOverlay = (_) => Console.WriteLine("Overlay not connected yet!");
        public static Action<string> SendToBroadcaster = (_) => Console.WriteLine("Broadcaster game not connected yet!");

        public static string CurrentScreenshotPath = Environment.CurrentDirectory + "/OBS_Screenshot";
        public static Action<string> OnScreenshotCaptured = (string fullpath) => Console.WriteLine("OnScreenshotCaptured not set!");
    }
}
