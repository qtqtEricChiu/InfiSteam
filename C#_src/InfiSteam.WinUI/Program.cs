#define DISABLE_XAML_GENERATED_MAIN
using Microsoft.UI.Xaml;
using System;
using System.Runtime.CompilerServices;

namespace InfiSteam.WinUI;

public static class Program
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.TryInitialize(0x00010006, out _);
    }

    [STAThread]
    static void Main(string[] args)
    {
        Application.Start(p =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
