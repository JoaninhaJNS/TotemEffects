using System.Windows;
using TotemEffects.Core;
using TotemEffects.UI;

namespace TotemEffects;

public partial class App : Application
{
    private Extension? ext;
    private MainWindow? window;
    private bool running;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Task.Run(Start);
    }

    private async void Start()
    {
        try
        {
            ext = new Extension();

            ext.Activated += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (window == null)
                    {
                        window = new MainWindow(ext);
                        window.Closing += OnClose;
                    }
                    window.Show();
                    window.Activate();
                });
            };

            running = true;
            ext.Run();
        }
        catch { }
        finally
        {
            running = false;
            await Task.Delay(2000);
            Dispatcher.Invoke(() => Shutdown());
        }
    }

    private void OnClose(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!running)
        {
            Shutdown();
        }
        else
        {
            e.Cancel = true;
            if (sender is Window w)
                w.Hide();
        }
    }
}