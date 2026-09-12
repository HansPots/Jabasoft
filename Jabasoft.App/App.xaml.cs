using System.Windows;
using Jabasoft.Base.AiBroker;

namespace Jabasoft.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Fire-and-forget, niet afgewacht: Jabasoft roept zelf nooit de AI
        // aan, alleen de apps die je er straks vanuit start doen dat - de
        // launcher-lijst hoeft niet te wachten tot de broker (die tot 45s
        // kan duren als 'm nog moet opstarten) bereikbaar is. Elke methode
        // hierin vangt zijn eigen fouten al af, dus hier is geen try/catch
        // nodig.
        _ = AiBrokerProcessLauncher.EnsureRunningAsync();
    }
}
