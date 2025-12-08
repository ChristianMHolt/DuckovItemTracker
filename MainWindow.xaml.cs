using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using ItemTracker.Models;
using ItemTracker.Services;
using ItemTracker.Utilities;

namespace ItemTracker;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ItemRepository _repository;
    private readonly ObservableCollection<Item> _items = new();
    private readonly ICollectionView _view;

    private string _nameText = string.Empty;
    private string _unitPriceText = string.Empty;
    private string _stackSizeText = "1";
    private string _weightText = "1.0";
    private string _iconLabel = "No icon selected";
    private string _currentIconPath = string.Empty;
    private string _searchText = string.Empty;
    private string _statusText = "Ready";
    private string _totalsText = "Total items: 0 (showing 0)";
    private Item? _selectedItem;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        _repository = new ItemRepository();
        DataContext = this;

        _view = CollectionViewSource.GetDefaultView(_items);
        _view.Filter = FilterItem;

        SortFields = new[] { "Name", "Unit Price", "Price per Stack", "Price per kg" };
        SortOrders = new[] { "Ascending", "Descending" };
        SelectedSortField = SortFields.First();
        SelectedSortOrder = SortOrders.First();

        LoadItems();
        RefreshView();
    }

    public IEnumerable<string> SortFields { get; }
    public IEnumerable<string> SortOrders { get; }

    public ObservableCollection<Item> FilteredItems => _items;

    public Item? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem == value) return;
            _selectedItem = value;
            OnPropertyChanged(nameof(SelectedItem));
            UpdateFormFromSelection();
        }
    }

    public string NameText
    {
        get => _nameText;
        set { _nameText = value; OnPropertyChanged(nameof(NameText)); }
    }

    public string UnitPriceText
    {
        get => _unitPriceText;
        set { _unitPriceText = value; OnPropertyChanged(nameof(UnitPriceText)); }
    }

    public string StackSizeText
    {
        get => _stackSizeText;
        set { _stackSizeText = value; OnPropertyChanged(nameof(StackSizeText)); }
    }

    public string WeightText
    {
        get => _weightText;
        set { _weightText = value; OnPropertyChanged(nameof(WeightText)); }
    }

    public string IconLabel
    {
        get => _iconLabel;
        set { _iconLabel = value; OnPropertyChanged(nameof(IconLabel)); }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            RefreshView(showMessage: true);
        }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
    }

    public string TotalsText
    {
        get => _totalsText;
        set { _totalsText = value; OnPropertyChanged(nameof(TotalsText)); }
    }

    public string AddOrUpdateButtonText => SelectedItem is null ? "Add Item" : "Update Item";

    public string SelectedSortField { get; set; }
    public string SelectedSortOrder { get; set; }

    private void LoadItems()
    {
        var loaded = _repository.Load();
        foreach (var item in loaded)
        {
            _items.Add(item);
        }
        StatusText = _items.Count > 0 ? $"Loaded {_items.Count} items from items.json" : "Ready";
    }

    private bool FilterItem(object obj)
    {
        if (obj is not Item item) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshView(bool showMessage = false)
    {
        _view.Refresh();
        UpdateTotals();
        OnPropertyChanged(nameof(AddOrUpdateButtonText));

        if (showMessage)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                StatusText = "Showing all items";
            }
            else
            {
                StatusText = $"Found {_view.Cast<Item>().Count()} item(s) matching '{SearchText}'";
            }
        }
    }

    private void UpdateTotals()
    {
        var showing = _view.Cast<Item>().Count();
        TotalsText = $"Total items: {_items.Count} (showing {showing})";
    }

    private void UpdateFormFromSelection()
    {
        if (SelectedItem is null)
        {
            ClearForm(keepStatus: true);
            return;
        }

        NameText = SelectedItem.Name;
        UnitPriceText = SelectedItem.UnitPrice.ToString("F2");
        StackSizeText = SelectedItem.StackSize.ToString();
        WeightText = SelectedItem.WeightPerItem.ToString("F3");
        _currentIconPath = SelectedItem.IconPath ?? string.Empty;
        IconLabel = string.IsNullOrWhiteSpace(_currentIconPath) ? "No icon selected" : Path.GetFileName(_currentIconPath);
        StatusText = $"Editing item: {SelectedItem.Name}";
        OnPropertyChanged(nameof(AddOrUpdateButtonText));
    }

    private bool TryParseForm(out Item item)
    {
        item = null!;

        if (string.IsNullOrWhiteSpace(NameText))
        {
            MessageBox.Show("Please enter an item name.", "Missing name", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!double.TryParse(UnitPriceText, out var unitPrice) ||
            !int.TryParse(StackSizeText, out var stackSize) ||
            !double.TryParse(WeightText, out var weight))
        {
            MessageBox.Show("Please enter valid numeric values for price, stack size, and weight.", "Invalid number", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        if (stackSize <= 0)
        {
            MessageBox.Show("Stack size must be greater than zero.", "Invalid stack size", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        if (weight <= 0)
        {
            MessageBox.Show("Weight per item must be greater than zero.", "Invalid weight", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        item = new Item
        {
            Name = NameText.Trim(),
            UnitPrice = unitPrice,
            StackSize = stackSize,
            WeightPerItem = weight,
            IconPath = _currentIconPath,
        };

        return true;
    }

    private void OnAddOrUpdate(object sender, RoutedEventArgs e)
    {
        if (!TryParseForm(out var newItem))
        {
            return;
        }

        var duplicate = _items
            .Select((it, idx) => (it, idx))
            .FirstOrDefault(pair => pair.it.Name.Equals(newItem.Name, StringComparison.OrdinalIgnoreCase));

        if (duplicate.it != null && duplicate.it != SelectedItem)
        {
            MessageBox.Show($"An item named '{newItem.Name}' already exists.", "Duplicate item", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var isNewItem = SelectedItem is null;

        if (isNewItem)
        {
            _items.Add(newItem);
            StatusText = $"Added item: {newItem.Name}";
        }
        else
        {
            var index = _items.IndexOf(SelectedItem);
            if (index >= 0)
            {
                _items[index] = newItem;
            }
            StatusText = $"Updated item: {newItem.Name}";
        }

        SaveItems();
        if (isNewItem)
        {
            _repository.CopyDataFileAndItemIcon(newItem);
        }
        RefreshView();
        ClearForm(keepStatus: true);
        FocusNameTextBox();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is null)
        {
            MessageBox.Show("Please select an item to delete.", "No selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var name = SelectedItem.Name;
        var result = MessageBox.Show($"Delete '{name}'?", "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _items.Remove(SelectedItem);
            SelectedItem = null;
            SaveItems();
            RefreshView();
            StatusText = $"Deleted item: {name}";
        }
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        ClearForm();
        FocusNameTextBox();
    }

    private void ClearForm(bool keepStatus = false)
    {
        NameText = string.Empty;
        UnitPriceText = string.Empty;
        StackSizeText = "1";
        WeightText = "1.0";
        _currentIconPath = string.Empty;
        IconLabel = "No icon selected";
        SelectedItem = null;
        if (!keepStatus)
        {
            StatusText = "Form cleared";
        }
        OnPropertyChanged(nameof(AddOrUpdateButtonText));
    }

    private void FocusNameTextBox()
    {
        if (NameTextBox.IsVisible)
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        }
    }

    private void OnChooseIcon(object sender, RoutedEventArgs e)
    {
        var initialDir = Directory.Exists(_repository.DefaultImageFolder)
            ? _repository.DefaultImageFolder
            : AppContext.BaseDirectory;

        var dialog = new OpenFileDialog
        {
            Title = "Select icon image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files (*.*)|*.*",
            InitialDirectory = initialDir,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            _currentIconPath = dialog.FileName;
            IconLabel = Path.GetFileName(dialog.FileName);
        }
    }

    private void OnApplySort(object sender, RoutedEventArgs e)
    {
        var ascending = SelectedSortOrder == "Ascending";
        var sorter = new ItemSorter(SelectedSortField, ascending);
        ApplySortToCollection(sorter);
        _view.Refresh();
        StatusText = $"Sorted by {SelectedSortField} ({SelectedSortOrder.ToLowerInvariant()})";
    }

    private void ApplySortToCollection(ItemSorter sorter)
    {
        var sorted = _items.OrderBy(item => item, sorter).ToList();
        _items.Clear();
        foreach (var item in sorted)
        {
            _items.Add(item);
        }
    }

    private void OnClearSearch(object sender, RoutedEventArgs e)
    {
        SearchText = string.Empty;
        RefreshView(showMessage: true);
    }

    private void OnVerifyClick(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(_repository.DefaultImageFolder))
        {
            MessageBox.Show($"Could not find image folder at {_repository.DefaultImageFolder}", "Missing folder", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var unused = _repository.FindUnusedImages(_items);
        var window = new VerificationWindow(unused)
        {
            Owner = this
        };
        window.ShowDialog();
        StatusText = "Verification complete";
    }

    private void SaveItems()
    {
        _repository.Save(_items);
    }

    private void OnWeightTextBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        WeightTextBox.SelectAll();
    }

    private void OnWeightTextBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!WeightTextBox.IsKeyboardFocusWithin)
        {
            WeightTextBox.Focus();
            e.Handled = true;
        }
    }

    private void OnStackSizeTextBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        StackSizeTextBox.SelectAll();
    }

    private void OnStackSizeTextBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!StackSizeTextBox.IsKeyboardFocusWithin)
        {
            StackSizeTextBox.Focus();
            e.Handled = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        SaveItems();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
