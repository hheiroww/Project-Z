using System.Collections.ObjectModel;
using Native = ProjectZ.Shared.Drawing.UI.Input;
using ProjectZ.Shared.Drawing.UI;
namespace ProjectZ.WpfCompatibility.Controls;
public class Menu : StackPanel
{
    public Menu() { Orientation = Orientation.Horizontal; }
    internal Menu(SceneElement native) : base(native) { Orientation = Orientation.Horizontal; }
    public UIElementCollection Items => Children;
}
public class ToggleButton : Button
{
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.RegisterNative(nameof(IsChecked), typeof(bool?), typeof(ToggleButton));
    public ToggleButton() : this(new Native.ToggleButton(Application.Current.ControlScene)) { }
    internal ToggleButton(SceneElement native) : base(native) { Listen("CheckedChanged", _ => { Changed(nameof(IsChecked)); Emit(IsChecked == true ? "Checked" : "Unchecked", Args()); }); }
    public bool? IsChecked { get => ((Native.ToggleButton)NativeElement).IsChecked; set => ((Native.ToggleButton)NativeElement).IsChecked = value == true; }
    public bool IsThreeState { get; set; }
    public event RoutedEventHandler? Checked { add { if (value != null) AddHandler(RoutedEvent.For("Checked"), value); } remove { if (value != null) RemoveHandler(RoutedEvent.For("Checked"), value); } }
    public event RoutedEventHandler? Unchecked { add { if (value != null) AddHandler(RoutedEvent.For("Unchecked"), value); } remove { if (value != null) RemoveHandler(RoutedEvent.For("Unchecked"), value); } }
}
public class TabItem : ContentControl
{
    object? header;
    bool requestedSelection;
    public TabItem() : base(new Native.Border(Application.Current.ControlScene)) { }
    internal TabItem(SceneElement native) : base(native) { }
    public object? Header { get => header; set { header = value; Changed(nameof(Header)); } }
    public bool IsSelected { get => Parent is TabControl tabs ? tabs.SelectedItem == this : requestedSelection; set { requestedSelection = value; if (Parent is TabControl tabs) { if (value) tabs.SelectedItem = this; else if (tabs.SelectedItem == this) tabs.SelectedIndex = -1; } Changed(nameof(IsSelected)); } }
    public override object? Content { get => LogicalChildren.FirstOrDefault(child => child != header); set { if (value is not FrameworkElement child) throw new ArgumentException("Tab content must be a native element."); if (ReferenceEquals(Content,child)) return; if (Content is FrameworkElement old) { NativeElement.Children.Remove(old.NativeElement); LogicalChildren.Remove(old); old.Reparent(null); } PrepareContentChild(child); if (!NativeElement.Children.Contains(child.NativeElement)) NativeElement.Children.Add(child.NativeElement); if (!LogicalChildren.Contains(child)) LogicalChildren.Add(child); child.Reparent(this); Changed(nameof(Content)); } }
    internal void SelectionChanged() => Changed(nameof(IsSelected));
}
public class TabControl : Control
{
    object? previousSelection;
    System.Collections.IEnumerable? itemsSource;
    System.Collections.Specialized.INotifyCollectionChanged? observableItems;
    Style? itemContainerStyle;
    public DataTemplate? ItemTemplate { get; set; }
    public string DisplayMemberPath { get; set; } = "";
    public Style? ItemContainerStyle { get => itemContainerStyle; set { itemContainerStyle=value; foreach(var tab in Items) value?.Apply(tab); } }
    public System.Collections.IEnumerable? ItemsSource
    {
        get => itemsSource;
        set { if(ReferenceEquals(value,itemsSource)) return; if(observableItems!=null) observableItems.CollectionChanged-=SourceChanged; itemsSource=value; observableItems=value as System.Collections.Specialized.INotifyCollectionChanged; if(observableItems!=null) observableItems.CollectionChanged+=SourceChanged; RefreshSource(); }
    }
    void SourceChanged(object? sender,System.Collections.Specialized.NotifyCollectionChangedEventArgs args) { if(Dispatcher.CheckAccess()) RefreshSource(); else _=Dispatcher.InvokeAsync(RefreshSource); }
    void RefreshSource()
    {
        Items.Clear(); if(itemsSource==null) return;
        using var construction=Application.Current.ConstructIn(NativeElement.OwnerScene);
        foreach(var item in itemsSource)
        {
            if(item is TabItem existing) { Items.Add(existing); continue; }
            var content=ItemTemplate?.LoadContent() ?? item as FrameworkElement ?? new TextBlock {Text=item?.ToString()??""}; content.DataContext=item;
            var header=DisplayMemberPath.Length==0 ? item : item?.GetType().GetProperty(DisplayMemberPath)?.GetValue(item);
            var tab=new TabItem {Header=header,Content=content,DataContext=item}; itemContainerStyle?.Apply(tab); Items.Add(tab);
        }
    }
    public TabControl() : this(new Native.TabControl(Application.Current.ControlScene)) { }
    internal TabControl(SceneElement native) : base(native)
    {
        Items = new(this);
        Listen("SelectionChanged", _ => { var selected = SelectedItem; var removed = previousSelection; previousSelection = selected; foreach (var tab in Items) tab.SelectionChanged(); Changed(nameof(SelectedIndex)); Changed(nameof(SelectedItem)); Emit("SelectionChanged", new SelectionChangedEventArgs { RemovedItems = removed == null ? Array.Empty<object>() : new[] { removed }, AddedItems = selected == null ? Array.Empty<object>() : new[] { selected } }); });
    }
    public TabItemCollection Items { get; }
    public int SelectedIndex { get => ((Native.TabControl)NativeElement).SelectedIndex; set => ((Native.TabControl)NativeElement).SelectedIndex = value; }
    public object? SelectedItem { get => SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null; set => SelectedIndex = value is TabItem tab ? Items.IndexOf(tab) : -1; }
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged
    {
        add { if (value != null) AddHandler(RoutedEvent.For("SelectionChanged"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("SelectionChanged"), value); }
    }
    internal void RefreshTabs() => ((Native.TabControl)NativeElement).SetTabs(Items.Select(tab => new Native.TabItem { Header = tab.Header is UIElement ? "" : tab.Header?.ToString() ?? "", HeaderContent = (tab.Header as UIElement)?.NativeElement, Content = tab.NativeElement, IsVisible = tab.Visibility == Visibility.Visible, IsEnabled = tab.IsEnabled }));
    public override void Dispose() { if(observableItems!=null) observableItems.CollectionChanged-=SourceChanged; Items.Detach(); base.Dispose(); }
}
public sealed class TabItemCollection(TabControl owner) : Collection<TabItem>
{
    void Watch(TabItem tab) { tab.Reparent(owner); owner.LogicalChildren.Add(tab); tab.PropertyChanged += Changed; }
    void Unwatch(TabItem tab) { tab.PropertyChanged -= Changed; owner.LogicalChildren.Remove(tab); tab.Reparent(null); }
    internal void Detach() { foreach (var tab in this) tab.PropertyChanged -= Changed; }
    void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs args) { if (args.PropertyName is "Header" or "Visibility" or "IsEnabled" or "Content") owner.RefreshTabs(); }
    protected override void InsertItem(int index, TabItem tab) { if (tab.Parent != null) throw new InvalidOperationException("The tab already has an owner."); var select = tab.IsSelected; base.InsertItem(index,tab); Watch(tab); owner.RefreshTabs(); if (select) owner.SelectedItem = tab; }
    protected override void RemoveItem(int index) { Unwatch(this[index]); base.RemoveItem(index); owner.RefreshTabs(); }
    protected override void ClearItems() { foreach (var tab in this) Unwatch(tab); base.ClearItems(); owner.RefreshTabs(); }
    protected override void SetItem(int index, TabItem tab) { if (tab == this[index]) return; if (tab.Parent != null) throw new InvalidOperationException("The tab already has an owner."); var select = tab.IsSelected; Unwatch(this[index]); base.SetItem(index, tab); Watch(tab); owner.RefreshTabs(); if (select) owner.SelectedItem = tab; }
}
