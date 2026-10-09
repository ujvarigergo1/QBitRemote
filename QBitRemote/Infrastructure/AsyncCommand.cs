using System.Windows.Input;
namespace QBitRemote.Infrastructure;
public sealed class AsyncCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null) : ICommand
{
    private bool _running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running && (canExecute?.Invoke(parameter) ?? true);
    public async void Execute(object? parameter) { _running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty); try { await execute(parameter); } finally { _running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); } }
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
