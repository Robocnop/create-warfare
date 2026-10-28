using CreateWarfare.Launcher.Core.Model;

namespace CreateWarfare.Launcher.ViewModels;

/// <summary>Une ligne de l'écran de revue.</summary>
public sealed class PlanRow
{
    public PlanRow(FileAction a)
    {
        Path = a.RelPath.Replace('/', '\\');
        Kind = a.Kind;
        Size = Format.Size(a.Size);
    }

    public string Path { get; }
    public FileActionKind Kind { get; }
    public string Size { get; }
    public bool Changes => Kind is FileActionKind.Add or FileActionKind.Replace or FileActionKind.Remove;

    public string Icon => Kind switch
    {
        FileActionKind.Add => "+",
        FileActionKind.Replace => "⇄",
        FileActionKind.Remove => "−",
        FileActionKind.Protected => "◆",
        _ => "=",
    };
}

public static class Format
{
    public static string Size(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} Go",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} Mo",
        >= 1L << 10 => $"{bytes / 1024.0:0} Ko",
        _ => $"{bytes} o",
    };
}
