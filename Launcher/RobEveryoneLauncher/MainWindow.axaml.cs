using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace RobEveryoneLauncher;

public partial class MainWindow : Window
{
    private readonly UpdateService updateService = new();

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        KeyDown += OnKeyDown;
    }

    // No window chrome by design (SystemDecorations="None" in the XAML) --
    // Escape is the only way out if something's gone wrong and the launcher
    // is sitting on an Error phase instead of auto-closing.
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        var progress = new Progress<UpdateProgress>(Report);

        string? executable;
        try
        {
            executable = await updateService.RunAsync(progress, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Report(new UpdateProgress { Phase = UpdatePhase.Error, Message = $"Update failed: {ex.Message}" });
            return;
        }

        if (executable == null)
        {
            // Report() already showed the Error phase's message for every
            // path that returns null -- nothing further to do here except
            // leave the window up so the player can actually read it.
            return;
        }

        UpdateService.LaunchGame(executable);

        // A short beat on the finished bar before closing reads far less
        // jarring than the window vanishing the instant the game process
        // starts.
        await Task.Delay(400);
        Close();
    }

    private void Report(UpdateProgress progress)
    {
        // Progress<T>'s callback already marshals back to the thread it was
        // constructed on (the UI thread, since it's built in OnOpened), but
        // Dispatcher.UIThread.Post is used anyway as a defensive no-op-if-
        // already-there -- cheap insurance against ever calling this from a
        // background thread by mistake later.
        Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = progress.Message;
            Progress.Value = progress.Fraction;

            if (progress.Phase == UpdatePhase.Error)
            {
                StatusText.Foreground = Brushes.OrangeRed;
                Progress.IsVisible = false;
            }
        });
    }
}
