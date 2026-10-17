using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using ProjectZ.Shared.Drawing.UI;
using Native = ProjectZ.Shared.Drawing.UI.Input;

namespace ProjectZ.WpfCompatibility.Controls;

/// <summary>Object items projected into native list and dropdown controls without losing their identity.</summary>
public class ItemsControl : Control
{
    readonly List<object?> selectedObjects = new();
    internal List<object?> SelectionSnapshot() => NativeElement is Native.ListBox list ? list.SelectedIndexes.Where(i=>i<displayedItems.Count).Select(i=>displayedItems[i]).ToList() : SelectedItem is {} item ? new(){item} : new();
    internal void SetSelection(IEnumerable<object?> items)
    {
        Dispatcher.VerifyAccess();var indexes=items.Select(item=>displayedItems.IndexOf(item)).ToArray();
        if(indexes.Any(i=>i<0)) throw new ArgumentException("Every selected item must belong to this control.");
        ((Native.ListBox)NativeElement).SetSelection(indexes);
    }
    readonly List<object?> directItems = new(), displayedItems = new();
    readonly List<FrameworkElement> itemViews = new();
    readonly List<ListBoxItem> styledContainers = new();
    Style? containerStyle;
    public Style? ItemContainerStyle { get => containerStyle; set { containerStyle = value; RefreshItems(); } }
    readonly HashSet<INotifyPropertyChanged> watchedItems = new(ReferenceEqualityComparer.Instance);
    IEnumerable? source;
    INotifyCollectionChanged? observable;
    DataTemplate? template;
    string displayPath = "", valuePath = "";
    int selectedIndex = -1;
    object? selectedItem;
    bool refreshing;
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.RegisterNative(nameof(ItemsSource), typeof(IEnumerable), typeof(ItemsControl));
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.RegisterNative(nameof(SelectedIndex), typeof(int), typeof(ItemsControl), new(-1));
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.RegisterNative(nameof(SelectedItem), typeof(object), typeof(ItemsControl));
    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.RegisterNative(nameof(SelectedValue), typeof(object), typeof(ItemsControl));
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged
    {
        add { if (value != null) AddHandler(RoutedEvent.For("SelectionChanged"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("SelectionChanged"), value); }
    }
    public ItemCollection Items { get; }
    public IReadOnlyList<FrameworkElement> ItemViews => itemViews;
    internal IReadOnlyList<object?> DisplayedItems => displayedItems;
    internal bool SourceBound => source != null;
    public ItemsControl() : this(new Native.ListBox(Application.Current.ControlScene)) { }
    internal ItemsControl(SceneElement native) : base(native)
    {
        Items = new(this);
        Listen("SelectionChanged", _ => { if (!refreshing) SynchronizeSelection(); });
        if (native is Native.ListBox list) { list.ItemIsEnabled = IsItemEnabled; list.ItemAppearance = ApplyContainerAppearance; }
        else if (native is Native.ComboBox combo) { combo.ItemIsEnabled = IsItemEnabled; combo.ItemAppearance = ApplyContainerAppearance; }
    }
    void ApplyContainerAppearance(int index, global::ProjectZ.Shared.Drawing.UI.Primitives.RectangleElement row, global::ProjectZ.Shared.Drawing.UI.Primitives.TextElement text)
    {
        if (index >= styledContainers.Count) return;
        var container = styledContainers[index];
        if (container.Background is Media.SolidColorBrush background) row.BackgroundColor = background.RenderColor;
        if (container.Foreground is Media.SolidColorBrush foreground) text.ForegroundColor = foreground.RenderColor;
    }
    bool IsItemEnabled(int index) => index >= 0 && index < displayedItems.Count && (displayedItems[index] is not UIElement item || item.IsEnabled) && (index >= styledContainers.Count || styledContainers[index].IsEnabled);
    public DataTemplate? ItemTemplate
    {
        get => template;
        set { Dispatcher.VerifyAccess(); if (this is ComboBox) throw new NotSupportedException("ComboBox item templates are not supported."); template = value; RefreshItems(); }
    }
    public IEnumerable? ItemsSource
    {
        get => source;
        set
        {
            Dispatcher.VerifyAccess();
            if (ReferenceEquals(value, source)) return;
            if (value != null && source == null && directItems.Count != 0) throw new InvalidOperationException("Items must be empty before setting ItemsSource.");
            if (observable != null) observable.CollectionChanged -= SourceChanged;
            source = value; observable = value as INotifyCollectionChanged;
            if (observable != null) observable.CollectionChanged += SourceChanged;
            RefreshItems(); Changed(nameof(ItemsSource));
        }
    }
    public string DisplayMemberPath { get => displayPath; set { Dispatcher.VerifyAccess(); displayPath = value ?? ""; RefreshItems(); } }
    public string SelectedValuePath { get => valuePath; set { Dispatcher.VerifyAccess(); valuePath = value ?? ""; Changed(nameof(SelectedValue)); } }
    public int SelectedIndex
    {
        get => (int)NativeElement.GetType().GetProperty(nameof(SelectedIndex))!.GetValue(NativeElement)!;
        set { Dispatcher.VerifyAccess(); NativeElement.GetType().GetProperty(nameof(SelectedIndex))!.SetValue(NativeElement, value); }
    }
#nullable disable
    // Preserve the legacy Selector contract without adding nullable warnings to
    // unchanged WPF source; no selection still returns null at runtime.
    public object SelectedItem { get => SelectedIndex >= 0 && SelectedIndex < displayedItems.Count ? displayedItems[SelectedIndex] : null; set => SelectedIndex = value == null ? -1 : displayedItems.IndexOf(value); }
#nullable restore
    public object? SelectedValue
    {
        get => SelectedItem == null ? null : ReadPath(SelectedItem, valuePath);
        set => SelectedIndex = value == null ? -1 : displayedItems.FindIndex(item => Equals(ReadPath(item, valuePath), value));
    }
    protected string DisplayText(object? item) => ReadPath(item, displayPath.Length == 0 && item is ContentControl ? nameof(ContentControl.Content) : displayPath)?.ToString() ?? "";
    static object? ReadPath(object? item, string path)
    {
        foreach (string part in path.Split('.', StringSplitOptions.RemoveEmptyEntries)) item = item?.GetType().GetProperty(part)?.GetValue(item);
        return item;
    }
    void SourceChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (Dispatcher.CheckAccess() && TryAppendTemplateItems(args)) return;
        RequestRefresh();
    }
    bool TryAppendTemplateItems(NotifyCollectionChangedEventArgs args)
    {
        if (IsDisposed || refreshing || template == null || source == null || NativeElement is not Native.ListBox list || args.Action != NotifyCollectionChangedAction.Add || args.NewItems == null || args.NewStartingIndex != displayedItems.Count || args.NewItems.Cast<object?>().Any(item => item is ListBoxItem)) return false;
        refreshing = true;
        try
        {
            foreach (var item in args.NewItems)
            {
                displayedItems.Add(item);
                if (containerStyle != null)
                {
                    using var construction = Application.Current.ConstructIn(NativeElement.OwnerScene);
                    ListBoxItem styled = this is ListView ? new ListViewItem() : new ListBoxItem();
                    styled.DataContext = item; containerStyle.Apply(styled); styledContainers.Add(styled);
                }
                if (item is INotifyPropertyChanged watched && watchedItems.Add(watched)) watched.PropertyChanged += ItemChanged;
                list.AddItem(DisplayText(item));
            }
        }
        finally { refreshing = false; }
        return true;
    }
    void ItemChanged(object? sender, PropertyChangedEventArgs args)
    {
        // Template bindings already observe their item. Rebuilding the complete
        // native list for each metric update discards live controls and textures.
        if (template == null && args.PropertyName != nameof(ListBoxItem.IsSelected)) RequestRefresh();
    }
    void RequestRefresh() { if (Dispatcher.CheckAccess()) RefreshItems(); else _ = Dispatcher.InvokeAsync(RefreshItems); }
    internal void MutateItems(Action<List<object?>> mutation)
    {
        Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (SourceBound) throw new InvalidOperationException("Items is read-only while ItemsSource is set.");
        var next = new List<object?>(directItems); mutation(next);
        var containers = next.OfType<ListBoxItem>().ToArray();
        if (containers.Distinct(ReferenceEqualityComparer.Instance).Count() != containers.Length || containers.Any(item => item.Parent != null && item.Parent != this)) throw new InvalidOperationException("An item container can only appear once in one control.");
        directItems.Clear(); directItems.AddRange(next); RefreshItems();
    }
    internal void RefreshItems()
    {
        Dispatcher.VerifyAccess(); if (IsDisposed || refreshing) return;
        refreshing = true;
        try
        {
            var previousSelection=SelectionSnapshot();
            foreach (var watched in watchedItems) watched.PropertyChanged -= ItemChanged;
            watchedItems.Clear();
            foreach (var old in displayedItems.OfType<ListBoxItem>()) { if (old.Parent == this) { LogicalChildren.Remove(old); old.Reparent(null); } }
            foreach (var view in itemViews) { LogicalChildren.Remove(view); view.Dispose(); }
            itemViews.Clear(); displayedItems.Clear();
            foreach (var container in styledContainers) container.Dispose();
            styledContainers.Clear();
            foreach (var item in source ?? directItems)
            {
                displayedItems.Add(item);
                if (containerStyle != null)
                {
                    using var construction = Application.Current.ConstructIn(NativeElement.OwnerScene);
                    ListBoxItem styled = this is ComboBox ? new ComboBoxItem() : this is ListView ? new ListViewItem() : new ListBoxItem();
                    styled.DataContext = item; containerStyle.Apply(styled); styledContainers.Add(styled);
                }
                if (item is INotifyPropertyChanged watched && watchedItems.Add(watched)) watched.PropertyChanged += ItemChanged;
                if (item is ListBoxItem container) { if (container.Parent != null && container.Parent != this) throw new InvalidOperationException("An item container already belongs to another control."); container.Reparent(this); if (!LogicalChildren.Contains(container)) LogicalChildren.Add(container); }
            }
            if (NativeElement is Native.ListBox list) list.ItemVisualFactory = template == null ? null : index =>
            {
                using var construction=Application.Current.ConstructIn(NativeElement.OwnerScene);
                var view = template.LoadContent(); view.Reparent(this); view.DataContext = displayedItems[index];
                view.IsHitTestVisible = false; LogicalChildren.Add(view); itemViews.Add(view); return view.NativeElement;
            };
            if (NativeElement is Native.ListBox virtualList) virtualList.ReleaseItemVisual = (_, element) =>
            {
                var view = itemViews.FirstOrDefault(candidate => ReferenceEquals(candidate.NativeElement, element));
                if (view == null) return;
                itemViews.Remove(view); LogicalChildren.Remove(view); view.Dispose();
            };
            NativeElement.GetType().GetProperty("Items")!.SetValue(NativeElement, displayedItems.Select(DisplayText).ToList());
            if(NativeElement is Native.ListBox multiple && multiple.SelectionMode!=0) SetSelection(previousSelection.Where(displayedItems.Contains));
            else SelectedIndex = selectedIndex < 0 ? -1 : displayedItems.IndexOf(selectedItem);
        }
        finally { refreshing = false; }
        SynchronizeSelection();
    }
    void SynchronizeSelection()
    {
        int nextIndex = SelectedIndex; object? nextItem = SelectedItem;
        var nextSelection=SelectionSnapshot();
        if (selectedIndex == nextIndex && ReferenceEquals(selectedItem, nextItem) && selectedObjects.SequenceEqual(nextSelection)) return;
        var removed=selectedObjects.Where(item=>!nextSelection.Contains(item)).ToArray();
        var added=nextSelection.Where(item=>!selectedObjects.Contains(item)).ToArray();
        foreach(var old in removed.OfType<ListBoxItem>()) old.SetSelected(false);
        selectedIndex = nextIndex; selectedItem = nextItem;
        selectedObjects.Clear();selectedObjects.AddRange(nextSelection);
        foreach(var current in added.OfType<ListBoxItem>()) current.SetSelected(true);
        Changed(nameof(SelectedIndex)); Changed(nameof(SelectedItem)); Changed(nameof(SelectedValue));
        Changed("SelectedItems");
        if (removed.Length!=0 || added.Length!=0)
            Emit("SelectionChanged", new SelectionChangedEventArgs { RemovedItems = removed, AddedItems = added });
    }
    public override void Dispose()
    {
        if (observable != null) observable.CollectionChanged -= SourceChanged;
        foreach (var watched in watchedItems) watched.PropertyChanged -= ItemChanged;
        watchedItems.Clear();
        foreach (var container in styledContainers) container.Dispose();
        styledContainers.Clear();
        if (NativeElement is Native.ListBox list) { list.ItemIsEnabled = null; list.ItemVisualFactory = null; list.ReleaseItemVisual = null; list.ItemAppearance = null; }
        else if (NativeElement is Native.ComboBox combo) { combo.IsDropDownOpen = false; combo.ItemIsEnabled = null; combo.ItemAppearance = null; }
        base.Dispose();
    }
}

public sealed class ItemCollection : IList
{
    readonly ItemsControl owner;
    internal ItemCollection(ItemsControl owner) => this.owner = owner;
    public int Count => owner.DisplayedItems.Count;
    public bool IsReadOnly => owner.SourceBound;
    public bool IsFixedSize => IsReadOnly;
    public bool IsSynchronized => false;
    public object SyncRoot => this;
    public object? this[int index] { get => owner.DisplayedItems[index]; set => owner.MutateItems(items => items[index] = value); }
    public int Add(object? value) { int index = Count; owner.MutateItems(items => items.Add(value)); return index; }
    public void Insert(int index, object? value) => owner.MutateItems(items => items.Insert(index, value));
    public void Clear() => owner.MutateItems(items => items.Clear());
    public void Remove(object? value) => owner.MutateItems(items => items.Remove(value));
    public void RemoveAt(int index) => owner.MutateItems(items => items.RemoveAt(index));
    public bool Contains(object? value) => IndexOf(value) >= 0;
    public int IndexOf(object? value) { for (int i = 0; i < Count; i++) if (Equals(this[i], value)) return i; return -1; }
    public void CopyTo(Array array, int index) { foreach (var item in owner.DisplayedItems) array.SetValue(item, index++); }
    public IEnumerator GetEnumerator() => owner.DisplayedItems.GetEnumerator();
    public void Refresh() => owner.RefreshItems();
}

public class ListBoxItem : Label
{
    bool selected;
    public bool IsSelected
    {
        get => selected;
        set { Dispatcher.VerifyAccess(); if (Parent is ListBox list && list.SelectionMode!=SelectionMode.Single) { if(value) list.SelectedItems.Add(this);else list.SelectedItems.Remove(this); } else if (Parent is ItemsControl owner) { if (value) owner.SelectedItem = this; else if (ReferenceEquals(owner.SelectedItem, this)) owner.SelectedIndex = -1; } else SetSelected(value); }
    }
    internal void SetSelected(bool value) { if (selected == value) return; selected = value; Changed(nameof(IsSelected)); }
    public override string ToString() => Content?.ToString() ?? "";
}
public class ComboBoxItem : ListBoxItem { }

internal sealed class SelectedItemCollection(ListBox owner) : IList
{
    List<object?> Snapshot=>owner.SelectionSnapshot();
    public int Count=>Snapshot.Count;
    public bool IsReadOnly=>false;
    public bool IsFixedSize=>false;
    public bool IsSynchronized=>false;
    public object SyncRoot=>this;
    public object? this[int index] {get=>Snapshot[index];set {var items=Snapshot;items[index]=value;owner.SetSelection(items);}}
    public int Add(object? value) {var items=Snapshot;int index=items.IndexOf(value);if(index>=0)return index;items.Add(value);owner.SetSelection(items);return items.Count-1;}
    public void Clear()=>owner.SetSelection(Array.Empty<object?>());
    public bool Contains(object? value)=>Snapshot.Contains(value);
    public int IndexOf(object? value)=>Snapshot.IndexOf(value);
    public void Insert(int index,object? value)=>Add(value);
    public void Remove(object? value) {var items=Snapshot;if(items.Remove(value))owner.SetSelection(items);}
    public void RemoveAt(int index) {var items=Snapshot;items.RemoveAt(index);owner.SetSelection(items);}
    public void CopyTo(Array array,int index)=>((ICollection)Snapshot).CopyTo(array,index);
    public IEnumerator GetEnumerator()=>Snapshot.GetEnumerator();
}
