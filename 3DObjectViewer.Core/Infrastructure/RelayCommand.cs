using System.Windows.Input;

namespace _3DObjectViewer.Core.Infrastructure;

/// <summary>
/// A reusable implementation of <see cref="ICommand"/> for the MVVM pattern.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a way to bind UI commands (such as button clicks) to methods
/// in the ViewModel without requiring code-behind event handlers.
/// </para>
/// <para>
/// The command automatically re-evaluates its <see cref="CanExecute"/> state when
/// WPF's <see cref="CommandManager.RequerySuggested"/> event is raised.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create a command with just an execute action
/// var simpleCommand = new RelayCommand(() => DoSomething());
/// 
/// // Create a command with execute and canExecute logic
/// var conditionalCommand = new RelayCommand(
///     () => Save(),
///     () => HasChanges);
/// </code>
/// </example>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelayCommand"/> class
    /// with a parameterized execute action.
    /// </summary>
    /// <param name="execute">
    /// The action to execute when the command is invoked.
    /// Receives the command parameter as an argument.
    /// </param>
    /// <param name="canExecute">
    /// An optional predicate that determines whether the command can execute.
    /// If <see langword="null"/>, the command is always enabled.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="execute"/> is <see langword="null"/>.
    /// </exception>
    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RelayCommand"/> class
    /// with a parameterless execute action.
    /// </summary>
    /// <param name="execute">The action to execute when the command is invoked.</param>
    /// <param name="canExecute">
    /// An optional function that determines whether the command can execute.
    /// If <see langword="null"/>, the command is always enabled.
    /// </param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    /// <summary>
    /// Occurs when changes occur that affect whether the command should execute.
    /// </summary>
    /// <remarks>
    /// This event is tied to <see cref="CommandManager.RequerySuggested"/>,
    /// which is raised whenever WPF determines that command bindings should be re-evaluated.
    /// </remarks>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// Determines whether the command can execute in its current state.
    /// </summary>
    /// <param name="parameter">
    /// Data used by the command. Can be <see langword="null"/> if the command does not require data.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the command can be executed; otherwise, <see langword="false"/>.
    /// </returns>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>
    /// Executes the command logic.
    /// </summary>
    /// <param name="parameter">
    /// Data used by the command. Can be <see langword="null"/> if the command does not require data.
    /// </param>
    public void Execute(object? parameter) => _execute(parameter);
}
