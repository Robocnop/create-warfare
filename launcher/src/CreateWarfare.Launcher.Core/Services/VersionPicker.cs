using CreateWarfare.Launcher.Core.Model;

namespace CreateWarfare.Launcher.Core.Services;

/// <param name="Channel">Channels.Release ou Channels.Prerelease.</param>
public sealed record VersionChoice(string Channel, string Label, VersionInfo Info)
{
    public bool IsPrerelease => Channel == Channels.Prerelease;
}

public static class VersionPicker
{
    /// <summary>Versions proposées : la dernière stable, puis la pré-release si le mode développeur est actif.</summary>
    public static List<VersionChoice> Choices(VersionInfo manifest, bool devMode)
    {
        var list = new List<VersionChoice> { new(Channels.Release, "Dernière version", manifest) };
        if (devMode && manifest.Prerelease is { Version.Length: > 0 } pre)
            list.Add(new(Channels.Prerelease, "Pré-release", pre));
        return list;
    }

    /// <summary>Le canal mémorisé s'il est encore proposé, sinon la dernière stable.</summary>
    public static VersionChoice Pick(VersionInfo manifest, bool devMode, string? channel)
    {
        var choices = Choices(manifest, devMode);
        return choices.FirstOrDefault(c => c.Channel == channel) ?? choices[0];
    }
}
