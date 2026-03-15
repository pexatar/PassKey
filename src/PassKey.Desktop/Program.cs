using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PassKey.Desktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // Note: ApplySavedLanguage() is called inside App() constructor before
        // InitializeComponent(), following the official Microsoft docs pattern.
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
