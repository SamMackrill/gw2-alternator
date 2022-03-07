namespace guildwars2.tools.alternator.MVVM.viewmodel;

/// <summary>
/// This class contains static references to all the view models in the
/// application and provides an entry point for the bindings.
/// </summary>
public class ViewModelLocator
{
    public MainViewModel MainWindowVM => Ioc.Default.GetRequiredService<MainViewModel>();
}
