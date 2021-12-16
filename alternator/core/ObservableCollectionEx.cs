namespace guildwars2.tools.alternator;

public class ObservableCollectionEx<T> : ObservableCollection<T>, IDisposable
{
    protected override event PropertyChangedEventHandler? PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    public void Dispose()
    {
        //seems like only existence of this allows to notify GC to clean up.
    }


    private bool notificationSupressed;
    private bool supressNotification;
    public bool SupressNotification
    {
        get
        {
            return supressNotification;
        }
        set
        {
            supressNotification = value;
            if (supressNotification == false && notificationSupressed)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                notificationSupressed = false;
            }
        }
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (SupressNotification)
        {
            notificationSupressed = true;
            return;
        }
        base.OnCollectionChanged(e);
    }

    /// <summary> 
    /// Adds the elements of the specified collection to the end of the ObservableCollection(Of T). 
    /// </summary> 
    public void AddRange(IEnumerable<T> collection)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));

        foreach (var i in collection) Items.Add(i);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary> 
    /// Removes the first occurence of each item in the specified collection from ObservableCollection(Of T). 
    /// </summary> 
    public void RemoveRange(IEnumerable<T> collection)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));

        foreach (var i in collection) Items.Remove(i);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary> 
    /// Clears the current collection and replaces it with the specified item. 
    /// </summary> 
    public void Replace(T item)
    {
        Replace(new[] { item });
    }

    /// <summary> 
    /// Replaces all elements in existing collection with specified collection of the ObservableCollection(Of T). 
    /// </summary> 
    public void Replace(IEnumerable<T> collection)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));

        Items.Clear();
        foreach (var i in collection) Items.Add(i);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary> 
    /// Initializes a new instance of the System.Collections.ObjectModel.ObservableCollection(Of T) class. 
    /// </summary> 
    public ObservableCollectionEx() { }

    /// <summary> 
    /// Initializes a new instance of the System.Collections.ObjectModel.ObservableCollection(Of T) class that contains elements copied from the specified collection. 
    /// </summary> 
    /// <param name="collection">collection: The collection from which the elements are copied.</param> 
    /// <exception cref="System.ArgumentNullException">The collection parameter cannot be null.</exception> 
    public ObservableCollectionEx(IEnumerable<T> collection)
        : base(collection) { }
}

