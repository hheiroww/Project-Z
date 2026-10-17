using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ProjectZ.WpfCompatibility.Data;

public readonly record struct SortDescription(string PropertyName, ListSortDirection Direction);
public sealed class SortDescriptionCollection : ObservableCollection<SortDescription> { }
public interface ICollectionView : IEnumerable, INotifyCollectionChanged
{
    IEnumerable SourceCollection { get; }
    Predicate<object>? Filter { get; set; }
    SortDescriptionCollection SortDescriptions { get; }
    bool CanFilter { get; }
    bool CanSort { get; }
    bool IsEmpty { get; }
    void Refresh();
}

public static class CollectionViewSource
{
    static readonly ConditionalWeakTable<object, NativeCollectionView> views = new();
    public static ICollectionView GetDefaultView(object source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source is ICollectionView view ? view : source is IEnumerable collection ? views.GetValue(source, _ => new(collection)) : throw new ArgumentException("A collection view requires an enumerable source.", nameof(source));
    }
}

/// <summary>Filtered, stable ordered original objects delivered to native item controls.</summary>
sealed class NativeCollectionView : ICollectionView
{
    object[] snapshot = [];
    Predicate<object>? filter;
    public IEnumerable SourceCollection { get; }
    public Predicate<object>? Filter { get => filter; set { filter=value; Refresh(); } }
    public SortDescriptionCollection SortDescriptions { get; }=new();
    public bool CanFilter => true;
    public bool CanSort => true;
    public bool IsEmpty => snapshot.Length==0;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public NativeCollectionView(IEnumerable source)
    {
        SourceCollection=source;
        if(source is INotifyCollectionChanged observable) observable.CollectionChanged+=(_,_)=>Refresh();
        SortDescriptions.CollectionChanged+=(_,_)=>Refresh();
        Refresh();
    }
    public void Refresh()
    {
        var items=SourceCollection.Cast<object>().Select((value,index)=>(Value:value,Index:index)).Where(item=>filter?.Invoke(item.Value)!=false).ToList();
        var sorting=SortDescriptions.ToArray();
        if(sorting.Length>0) items.Sort((left,right)=>
        {
            foreach(var description in sorting)
            {
                var a=Property(left.Value,description.PropertyName);var b=Property(right.Value,description.PropertyName);
                int result=ReferenceEquals(a,b)?0:a==null?-1:b==null?1:a is string text && b is string other?StringComparer.CurrentCulture.Compare(text,other):a is IComparable comparable?comparable.CompareTo(b):StringComparer.CurrentCulture.Compare(Convert.ToString(a,CultureInfo.CurrentCulture),Convert.ToString(b,CultureInfo.CurrentCulture));
                if(result!=0) return description.Direction==ListSortDirection.Descending?-Math.Sign(result):Math.Sign(result);
            }
            return left.Index.CompareTo(right.Index);
        });
        snapshot=items.Select(item=>item.Value).ToArray();
        CollectionChanged?.Invoke(this,new(NotifyCollectionChangedAction.Reset));
    }
    static object? Property(object? value,string path)
    {
        foreach(var name in path.Split('.',StringSplitOptions.RemoveEmptyEntries)) value=value?.GetType().GetProperty(name)?.GetValue(value);
        return value;
    }
    public IEnumerator GetEnumerator()=>snapshot.GetEnumerator();
}
