using System.Collections.ObjectModel;
namespace ProjectZ.WpfCompatibility;
public class Setter
{
    public DependencyProperty Property { get; set; }
    public object? Value { get; set; }
    public string? TargetName { get; set; }
    public Setter(DependencyProperty property,object? value) { Property=property;Value=value; }
}
public class SetterBaseCollection : Collection<Setter>
{
    internal Action<Setter>? Added;
    protected override void InsertItem(int index,Setter setter) { base.InsertItem(index,setter);Added?.Invoke(setter); }
}
public class Style
{
    public Type? TargetType { get; set; }
    public Style? BasedOn { get; set; }
    public SetterBaseCollection Setters { get; }=new();
    public bool IsSealed { get; private set; }
    public Style() { }
    public Style(Type targetType)=>TargetType=targetType;
    public Style(Type targetType,Style basedOn) : this(targetType)=>BasedOn=basedOn;
    internal string? ResourceKey;
    internal string? SourcePath;
    internal void ConnectNativeResource(string key,string path)
    {
        ResourceKey=key;SourcePath=path;
        Setters.Added=setter=>global::ProjectZ.Shared.Drawing.Designer.SceneXamlParser.UpdateRegisteredStyle(path,key,setter.Property.Name,Convert.ToString(setter.Value,System.Globalization.CultureInfo.InvariantCulture));
    }
    internal void Apply(FrameworkElement target)
    {
        if(TargetType!=null&&!TargetType.IsInstanceOfType(target)) throw new ArgumentException("The style targets "+TargetType.Name);
        BasedOn?.Apply(target);
        foreach(var setter in Setters)
        {
            var recipient = (setter.TargetName!=null?target.FindName(setter.TargetName) as DependencyObject:target)??throw new InvalidOperationException("Style target is missing.");
            if (setter.Value is Data.Binding binding) Data.BindingOperations.SetBinding(recipient, setter.Property, binding);
            else recipient.SetValue(setter.Property,setter.Value);
        }
        IsSealed=true;
    }
}
