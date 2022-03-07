namespace guildwars2.tools.alternator;

public interface ILoadedAction
{
    void WindowLoaded();
}

public class DelegateLoadedAction : ILoadedAction
{
    public Action? LoadedActionDelegate { get; }

    public DelegateLoadedAction(Action action)
    {
        LoadedActionDelegate = action;
    }

    public void WindowLoaded()
    {
        LoadedActionDelegate?.Invoke();
    }
}

public class LoadedBindings
{

    private static void Loaded(object sender, RoutedEventArgs e)
    {
        var loadedAction = GetLoadedAction((Window)sender);
        loadedAction.WindowLoaded();
    }

    public static readonly DependencyProperty LoadedActionProperty =
        DependencyProperty.RegisterAttached(
            "LoadedAction",
            typeof(ILoadedAction),
            typeof(LoadedBindings),
            new PropertyMetadata(null));

    public static ILoadedAction GetLoadedAction(DependencyObject sender) => (ILoadedAction)sender.GetValue(LoadedActionProperty);
    public static void SetLoadedAction(DependencyObject sender, ILoadedAction value) => sender.SetValue(LoadedActionProperty, value);
}