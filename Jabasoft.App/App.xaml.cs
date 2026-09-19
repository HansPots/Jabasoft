using System.Windows;
using Jabasoft.Base;
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

        // Vóór het venster: taal, thema, lettertype, marges en ververstijd
        // staan dan meteen goed in plaats van dat je het scherm ziet
        // omklappen. Zie Layout/Preferences.
        Layout.Preferences.Apply(this);

        // Fire-and-forget, niet afgewacht: Jabasoft roept zelf nooit de AI
        // aan, alleen de apps die je er straks vanuit start doen dat - de
        // launcher-lijst hoeft niet te wachten tot de broker (die tot 45s
        // kan duren als 'm nog moet opstarten) bereikbaar is. Elke methode
        // hierin vangt zijn eigen fouten al af, dus hier is geen try/catch
        // nodig.
        _ = AiBrokerProcessLauncher.EnsureRunningAsync();
    }

    /// <summary>
    /// Ruimt de broker op als Jabasoft de laatste applicatie van de familie
    /// was die draaide. Draait er nog een andere, dan blijft hij staan -
    /// die heeft hem nodig.
    ///
    /// Synchroon en niet fire-and-forget: na deze methode wordt het proces
    /// afgebroken, dus een taak die nog moet lopen komt er niet meer aan
    /// toe. Het is een paar procesvragen, dat merk je niet bij het sluiten.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        // Vangnet: staat er een applicatie van de familie verborgen omdat wij
        // het scherm van haar overgenomen hebben, dan komt ze nu terug. Zonder
        // dit zou ze onzichtbaar blijven draaien, zonder taakbalkknop en dus
        // zonder weg terug.
        AppHandover.RevealHidden();

        AiBrokerProcessLauncher.StopIfUnused();

        base.OnExit(e);
    }
}
