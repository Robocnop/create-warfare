using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CreateWarfare.Launcher.ViewModels;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}

/// <summary>Commande qui accepte une action synchrone ou asynchrone ; une seule exécution à la fois.</summary>
public sealed class Command : ICommand
{
    readonly Func<Task> _run;
    readonly Func<bool>? _can;
    bool _running;

    public Command(Func<Task> run, Func<bool>? can = null) { _run = run; _can = can; }
    public Command(Action run, Func<bool>? can = null) : this(() => { run(); return Task.CompletedTask; }, can) { }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_running && (_can?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        _running = true;
        CommandManager.InvalidateRequerySuggested();
        try { await _run(); }
        finally
        {
            _running = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
