using WeakEventManager = AsyncAwaitBestPractices.WeakEventManager;

namespace guildwars2.tools.alternator;

public interface ICommandExtended : ICommand
{
    /// <summary>Raises the CanExecuteChanged event.</summary>
    void RaiseCanExecuteChanged();
}

/// <summary>
/// A command to relay its functionality to other objects by invoking delegates. The default return value for the CanExecute method is 'true'.
/// </summary>
public class RelayCommand<T> : ICommandExtended
{

    private readonly Predicate<T?> canExecute;
    private readonly Action<T?> execute;
    private readonly WeakEventManager weakEventManager = new();

    /// <summary>
    /// Initializes BaseCommand
    /// </summary>
    /// <param name="execute"></param>
    /// <param name="canExecute"></param>
    public RelayCommand(Action<T?>? execute, Predicate<T?>? canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute ?? (_ => true) ;
    }
    /// <summary>
    /// Occurs when changes occur that affect whether or not the command should execute
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => weakEventManager.AddEventHandler(value);
        remove => weakEventManager.RemoveEventHandler(value);
    }

    /// <summary>
    /// Determines whether the command can execute in its current state
    /// </summary>
    /// <returns><c>true</c>, if this command can be executed; otherwise, <c>false</c>.</returns>
    /// <param name="parameter">Data used by the command. If the command does not require data to be passed, this object can be set to null.</param>
    bool ICommand.CanExecute(object? parameter) => parameter switch
    {
        T validParameter => CanExecute(validParameter),
        null when IsNullable<T>() => CanExecute((T?)parameter),
        null => throw new InvalidCommandParameterException(typeof(T)),
        _ => throw new InvalidCommandParameterException(typeof(T), parameter.GetType()),
    };

    /// <summary>
    /// Determines whether the command can execute in its current state
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public bool CanExecute(T? parameter) => canExecute(parameter);

    void ICommand.Execute(object? parameter)
    {
        switch (parameter)
        {
            case T validParameter:
                Execute(validParameter);
                break;

            case null when IsNullable<T>():
                Execute((T?)parameter);
                break;

            case null:
                throw new InvalidCommandParameterException(typeof(T));

            default:
                throw new InvalidCommandParameterException(typeof(T), parameter.GetType());
        }
    }

    /// <summary>
    /// Execute
    /// </summary>
    /// <param name="parameter"></param>
    public void Execute(T? parameter) => execute(parameter);

    /// <summary>
    /// Raises the CanExecuteChanged event.
    /// </summary>
    public void RaiseCanExecuteChanged() => weakEventManager.RaiseEvent(this, EventArgs.Empty, nameof(CanExecuteChanged));

    /// <summary>
    /// Determine if TN is Nullable
    /// </summary>
    /// <typeparam name="TN"></typeparam>
    /// <returns></returns>
    private static bool IsNullable<TN>()
    {
        var type = typeof(TN);
        return !type.GetTypeInfo().IsValueType || Nullable.GetUnderlyingType(type) != null;
    }
}

