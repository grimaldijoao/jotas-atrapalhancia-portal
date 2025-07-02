using Headless.Shared;
using Headless.Shared.Utils;

namespace ScreenshotHandler
{
    public class ScreenshotListener
    {
        FileSystemWatcher watcher;
        CancellationTokenSource? cancelTokenSource = null;

        public void Init()
        {
            //watcher = new FileSystemWatcher();
            //
            //watcher.Path = External.CurrentScreenshotPath;
            //
            //watcher.NotifyFilter = NotifyFilters.LastWrite;
            //
            //watcher.Filter = "*.png";
            //
            //watcher.Changed += new FileSystemEventHandler(OnChanged);
            //
            //watcher.EnableRaisingEvents = true;
            //
            //Console.WriteLine($"Listening for current.png at {watcher.Path}");
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            //if (e.ChangeType == WatcherChangeTypes.Changed && e.Name == "current.png")
            //{
            //    Action raiseCaptured = () =>
            //    {
            //        External.OnScreenshotCaptured(e.FullPath);
            //    };
            //    raiseCaptured.Debounce(ref cancelTokenSource, 2000);
            //}
        }
    }
}
