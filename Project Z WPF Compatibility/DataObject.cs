using global::ProjectZ.Shared.Drawing.UI;
using DesktopData = System.Windows.Forms.IDataObject;

namespace ProjectZ.WpfCompatibility;

public delegate void DataObjectPastingEventHandler(object sender, DataObjectPastingEventArgs e);

/// <summary>Desktop clipboard data with the native Project-Z paste command event.</summary>
public class DataObject : System.Windows.Forms.DataObject
{
    public DataObject() { }
    public DataObject(object data) : base(DesktopDataAdapter.ToDesktop(data)!) { }
    public DataObject(string format, object data) : base(format, DesktopDataAdapter.ToDesktop(data) ?? throw new ArgumentNullException(nameof(data))) { }
    public override void SetData(string format, bool autoConvert, object? data) => base.SetData(format, autoConvert, DesktopDataAdapter.ToDesktop(data));
    public override void SetData(string format, object? data) => SetData(format, true, data);
    public override void SetData(Type format, object? data) { if (format==typeof(Media.Imaging.BitmapSource)) SetData(System.Windows.Forms.DataFormats.Bitmap, data); else base.SetData(format, DesktopDataAdapter.ToDesktop(data)); }
    public override void SetData(object? data) => base.SetData(DesktopDataAdapter.ToDesktop(data));
    public static readonly RoutedEvent PastingEvent = new("Pasting", RoutingStrategy.Bubble);
    public static void AddPastingHandler(DependencyObject element, DataObjectPastingEventHandler handler) => ((UIElement)element).AddHandler(PastingEvent, handler);
    public static void RemovePastingHandler(DependencyObject element, DataObjectPastingEventHandler handler) => ((UIElement)element).RemoveHandler(PastingEvent, handler);
}

public class DataObjectEventArgs : RoutedEventArgs
{
    internal readonly ScenePastingContext Context;
    public bool IsDragDrop { get; }
    internal DataObjectEventArgs(ScenePastingContext context, bool isDragDrop=false) : base(DataObject.PastingEvent) { Context=context; IsDragDrop=isDragDrop; }
    public void CancelCommand() => Context.Cancelled=true;
}

public sealed class DataObjectPastingEventArgs : DataObjectEventArgs
{
    internal DataObjectPastingEventArgs(ScenePastingContext context) : base(context) { }
    public DataObjectPastingEventArgs(DesktopData dataObject, bool isDragDrop, string formatToApply) : base(new(dataObject, formatToApply), isDragDrop) { }
    public DesktopData SourceDataObject => new DesktopDataAdapter((DesktopData)Context.SourceDataObject);
    public DesktopData DataObject { get => new DesktopDataAdapter((DesktopData)Context.DataObject); set => Context.DataObject=DesktopDataAdapter.Unwrap(value ?? throw new ArgumentNullException(nameof(value))); }
    public string FormatToApply { get => Context.FormatToApply; set => Context.FormatToApply=value ?? throw new ArgumentNullException(nameof(value)); }
}

/// <summary>Expose native bitmap pixels through the imported BitmapSource contract.</summary>
internal sealed class DesktopDataAdapter(DesktopData data) : DesktopData
{
    internal static object Unwrap(object value) => value is DesktopDataAdapter adapter ? adapter.Data : ToDesktop(value)!;
    DesktopData Data => data;
    internal static object? ToDesktop(object? value) => value is DesktopDataAdapter adapter ? adapter.Data : value is Media.Imaging.BitmapSource source ? source.ToBitmap() : value;
    static object? ToImported(object? value) => value is System.Drawing.Bitmap bitmap ? Media.Imaging.BitmapSource.FromDesktopBitmap(bitmap) : value;
    public object? GetData(string format, bool autoConvert) => ToImported(data.GetData(format, autoConvert));
    public object? GetData(string format) => GetData(format, true);
    public object? GetData(Type format) => format==typeof(Media.Imaging.BitmapSource) ? GetData(System.Windows.Forms.DataFormats.Bitmap) : ToImported(data.GetData(format));
    public bool GetDataPresent(string format, bool autoConvert) => data.GetDataPresent(format, autoConvert);
    public bool GetDataPresent(string format) => data.GetDataPresent(format);
    public bool GetDataPresent(Type format) => format==typeof(Media.Imaging.BitmapSource) ? GetDataPresent(System.Windows.Forms.DataFormats.Bitmap) : data.GetDataPresent(format);
    public string[] GetFormats(bool autoConvert) => data.GetFormats(autoConvert);
    public string[] GetFormats() => data.GetFormats();
    public void SetData(string format, bool autoConvert, object? value) => data.SetData(format, autoConvert, ToDesktop(value));
    public void SetData(string format, object? value) => data.SetData(format, ToDesktop(value));
    public void SetData(Type format, object? value) { if(format==typeof(Media.Imaging.BitmapSource)) SetData(System.Windows.Forms.DataFormats.Bitmap,value); else data.SetData(format,ToDesktop(value)); }
    public void SetData(object? value) => data.SetData(ToDesktop(value));
}
