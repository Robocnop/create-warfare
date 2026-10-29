using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.ViewModels;

/// <summary>Une ligne du sélecteur de version, comme dans le launcher Minecraft.</summary>
public sealed class VersionItem(VersionChoice choice, Action<VersionChoice> select)
{
    public VersionChoice Choice { get; } = choice;
    public string Label => Choice.Label;
    public string Version => Choice.Info.Version;
    public bool IsPrerelease => Choice.IsPrerelease;
    public bool IsRelease => !Choice.IsPrerelease;
    public Command SelectCommand { get; } = new(() => select(choice));
}
