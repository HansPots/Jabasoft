using System;

namespace Jabasoft.App;

/// <summary>
/// Lets the BlazorWebView-hosted token dashboard ask the WPF host to
/// switch back to the HTML shell (menu, embedded apps, Stijlgids) - the
/// dashboard doesn't have its own way to reach a sibling native control,
/// so this is the one narrow channel between the two. Registered as a
/// singleton in the same DI container the BlazorWebView uses (see
/// MainWindow.xaml.cs).
/// </summary>
public sealed class JabasoftHostBridge
{
    public event Action? BackToShellRequested;

    public void BackToShell() => BackToShellRequested?.Invoke();
}
