using System.Collections.ObjectModel;
using System.ComponentModel;
namespace ImportSamples;
public sealed class Person : INotifyPropertyChanged
{
    string name = "Ada";
    public string Name { get => name; set { if (name == value) return; name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed class Model : INotifyPropertyChanged
{
    Person person = new(); bool enabled;
    public Person Person { get => person; set { person = value; PropertyChanged?.Invoke(this, new(nameof(Person))); } }
    public bool Enabled { get => enabled; set { enabled = value; PropertyChanged?.Invoke(this, new(nameof(Enabled))); } }
    public ObservableCollection<string> Items { get; } = new() { "first", "second" };
    public event PropertyChangedEventHandler? PropertyChanged;
}
