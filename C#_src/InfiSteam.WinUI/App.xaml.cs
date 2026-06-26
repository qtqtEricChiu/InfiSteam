using Microsoft.UI.Xaml;

namespace InfiSteam.WinUI;

public partial class App : Application
{
    private Window? _mainWindow;

    public static Window? MainWindowInstance { get; private set; }

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _mainWindow = new MainWindow();
        MainWindowInstance = _mainWindow;
        _mainWindow.Activate();
    }
}
