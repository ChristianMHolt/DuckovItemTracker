using System.Windows;

namespace ItemTracker;

public partial class App : Application
{
    public App()
    {
        // Ensure application resources declared in App.xaml are loaded
        // before we create and show the main window.
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
