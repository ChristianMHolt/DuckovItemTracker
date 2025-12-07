using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace ItemTracker;

public partial class VerificationWindow : Window, INotifyPropertyChanged
{
    private string _headerText = string.Empty;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> UnusedImages { get; } = new();

    public string HeaderText
    {
        get => _headerText;
        set { _headerText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderText))); }
    }

    public VerificationWindow(IEnumerable<string> unusedImages)
    {
        InitializeComponent();
        DataContext = this;

        foreach (var img in unusedImages)
        {
            UnusedImages.Add(img);
        }

        HeaderText = $"Unused images: {UnusedImages.Count}";

        if (!UnusedImages.Any())
        {
            UnusedImages.Add("All images in ItemPNGS are referenced by items.json.");
        }
    }
}
