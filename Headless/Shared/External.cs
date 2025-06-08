namespace Headless.Shared
{
    public class External
    {
        public static Dictionary<string, Action<byte[]>> SendToOverlay = new Dictionary<string, Action<byte[]>>();
        public static Dictionary<string, Action<string>> SendToBroadcaster = new Dictionary<string, Action<string>>();

        public static string CurrentScreenshotPath = Environment.CurrentDirectory + "/OBS_Screenshot";
        public static Action<string> OnScreenshotCaptured = (string fullpath) => Console.WriteLine("OnScreenshotCaptured not set!");
    }
}
