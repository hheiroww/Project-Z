using System.ComponentModel;
using System.Xml.Linq;
using ProjectZ.Shared.Drawing.Designer;
using ProjectZ.WpfCompatibility.Data;

namespace ProjectZ.WpfCompatibility;

/// <summary>A data trigger observes the same model as native bindings and restores its scalar setters on exit.</summary>
public sealed class NativeDataTrigger : IDisposable
{
    readonly FrameworkElement owner;
    readonly XElement declaration;
    readonly SceneXamlParser parser;
    readonly Binding binding;
    readonly List<INotifyPropertyChanged> watched = new();
    readonly List<(FrameworkElement Target, string Property)> applied = new();
    static int nextPriority;
    readonly int priority=Interlocked.Increment(ref nextPriority);
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement,Dictionary<string,SetterLayers>> overrides=new();
    sealed class SetterLayers(object? initial)
    {
        public object? Base=initial;
        public readonly Dictionary<NativeDataTrigger,object?> Values=new();
    }
    void ApplySetter(FrameworkElement target,string property,object? value)
    {
        var properties=overrides.GetOrCreateValue(target);
        if(!properties.TryGetValue(property,out var layers)) properties[property]=layers=new(target.GetType().GetProperty(property)!.GetValue(target));
        layers.Values[this]=value;applied.Add((target,property));
        XamlLoader.SetProperty(target,property,layers.Values.OrderBy(v=>v.Key.priority).Last().Value);
    }
    void RestoreSetters()
    {
        foreach(var item in applied.AsEnumerable().Reverse())
        {
            var properties=overrides.GetOrCreateValue(item.Target);var layers=properties[item.Property];layers.Values.Remove(this);
            var value=layers.Values.Count==0?layers.Base:layers.Values.OrderBy(v=>v.Key.priority).Last().Value;
            if(layers.Values.Count==0) properties.Remove(item.Property);
            XamlLoader.SetProperty(item.Target,item.Property,value);
        }
        applied.Clear();
    }
    bool active, updating, disposed;
    public NativeDataTrigger(FrameworkElement owner, XElement declaration, SceneXamlParser parser)
    {
        this.owner = owner; this.declaration = new(declaration); this.parser = parser;
        binding = declaration.Name.LocalName=="Trigger" ? new Binding((string?)declaration.Attribute("Property")??"") { RelativeSource=RelativeSource.Self,ElementName=(string?)declaration.Attribute("SourceName") } : XamlLoader.ParseBinding((string?)declaration.Attribute("Binding") ?? "{Binding}", owner);
        owner.PropertyChanged += OwnerChanged;
        if(declaration.Name.LocalName=="Trigger" && declaration.Elements().Any(n=>n.Name.LocalName=="Setter" && (string?)n.Attribute("Property")=="Background") && owner.NativeElement is global::ProjectZ.Shared.Drawing.UI.Input.Button button) { button.UseImportedStateBackground=true;button.isAnimated=false; }
        Update();
    }
    void OwnerChanged(object? sender, PropertyChangedEventArgs args) { if (declaration.Name.LocalName=="Trigger" || args.PropertyName == nameof(FrameworkElement.DataContext)) Update(); }
    void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (owner.Dispatcher.CheckAccess()) Update(); else _ = owner.Dispatcher.InvokeAsync(Update);
    }
    void Update()
    {
        if (updating || disposed) return;
        updating = true;
        try
        {
            foreach (var item in watched) item.PropertyChanged -= ModelChanged; watched.Clear();
            object? value = binding.HasSource ? binding.Source : binding.ElementName != null ? owner.FindName(binding.ElementName) : binding.RelativeSource?.Mode == RelativeSourceMode.Self ? owner : owner.DataContext;
            if (binding.RelativeSource?.Mode == RelativeSourceMode.FindAncestor)
            {
                value = null; int level = binding.RelativeSource.AncestorLevel;
                for (var parent = owner.Parent; parent != null; parent = parent.Parent)
                    if ((binding.RelativeSource.AncestorType == null || binding.RelativeSource.AncestorType.IsInstanceOfType(parent)) && --level == 0) { value = parent; break; }
            }
            Watch(value);
            foreach (var part in binding.Path.Path.Split('.', StringSplitOptions.RemoveEmptyEntries)) { value = value?.GetType().GetProperty(part)?.GetValue(value); Watch(value); }
            if (binding.Converter != null) value = binding.Converter.Convert(value, typeof(object), binding.ConverterParameter, System.Globalization.CultureInfo.CurrentCulture);
            var expected=(string?)declaration.Attribute("Value");
            bool next = expected=="{x:Null}" ? value==null : string.Equals(System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), expected, StringComparison.OrdinalIgnoreCase);
            if (next == active) return;
            active = next;
            if (next)
            {
                foreach (var setter in declaration.Elements().Where(n => n.Name.LocalName == "Setter"))
                {
                    var target = (string?)setter.Attribute("TargetName") is { } name ? owner.FindName(name) as FrameworkElement ?? throw new InvalidDataException("Trigger target " + name) : owner;
                    var property = (string?)setter.Attribute("Property") ?? throw new InvalidDataException("Trigger setter property");
                    var member = target.GetType().GetProperty(property) ?? throw new MissingMemberException(target.GetType().Name, property);
                    var text=(string?)setter.Attribute("Value");
                    ApplySetter(target,property,text?.StartsWith("{StaticResource ")==true ? target.FindResource(text[16..^1].Trim()) : text);
                }
            }
            else RestoreSetters();
            foreach (var action in declaration.Elements().Where(n => n.Name.LocalName.EndsWith(next ? ".EnterActions" : ".ExitActions", StringComparison.Ordinal)).SelectMany(n => n.Elements()))
            {
                if (action.Name.LocalName == "BeginStoryboard")
                {
                    if (action.Elements().FirstOrDefault(n => n.Name.LocalName == "Storyboard") is { } storyboard) parser.BeginImportedStoryboard(storyboard.ToString(), owner.Name);
                    else if ((string?)action.Attribute("Storyboard") is { } reference) parser.BeginStoryboard(reference.Replace("{StaticResource ", "").TrimEnd('}'));
                }
            }
        }
        finally { updating = false; }
    }
    void Watch(object? value) { if (value is INotifyPropertyChanged notify && !watched.Contains(notify)) { watched.Add(notify); notify.PropertyChanged += ModelChanged; } }
    public void Dispose() { if (disposed) return; disposed = true; owner.PropertyChanged -= OwnerChanged; foreach (var item in watched) item.PropertyChanged -= ModelChanged; watched.Clear(); RestoreSetters(); }
}
