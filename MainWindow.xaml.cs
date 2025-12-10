using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Text.RegularExpressions;
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
    private readonly List<string> _allNameSuggestions = new();
    private readonly ObservableCollection<string> _filteredNameSuggestions = new();
    private readonly Dictionary<string, string> _suggestionToImagePath = new(StringComparer.OrdinalIgnoreCase);

    public static RoutedUICommand AddOrUpdateCommand { get; } = new(
        "Add or Update Item",
        nameof(AddOrUpdateCommand),
        typeof(MainWindow));

    private string _nameText = string.Empty;
    private string _unitPriceText = string.Empty;
    private string _stackSizeText = "1";
    private string _weightText = "1.0";
    private string _durabilityText = string.Empty;
    private string _maxDurabilityText = string.Empty;
    private string _durabilityPercentText = string.Empty;
    private string _nameAvailabilityText = string.Empty;
    private string _iconLabel = "No icon selected";
    private string _currentIconPath = string.Empty;
    private string _searchText = string.Empty;
    private string _statusText = "Ready";
    private string _totalsText = "Total items: 0 (showing 0)";
    private string _selectedDurabilityRange = string.Empty;
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
        DurabilityRanges = new[] { "All", "1-25%", "26-50%", "51-75%", "76-100%", "100%" };
        SelectedSortField = SortFields.First();
        SelectedSortOrder = SortOrders.First();
        _selectedDurabilityRange = DurabilityRanges.First();

        LoadNameSuggestions();

        LoadItems();
        RefreshView();
    }

    public IEnumerable<string> SortFields { get; }
    public IEnumerable<string> SortOrders { get; }
    public IEnumerable<string> DurabilityRanges { get; }

    public ObservableCollection<Item> FilteredItems => _items;
    public ObservableCollection<string> FilteredNameSuggestions => _filteredNameSuggestions;

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
        set
        {
            if (_nameText == value)
            {
                return;
            }

            _nameText = value;
            OnPropertyChanged(nameof(NameText));
            UpdateDurabilitySummary();
            RefreshNameSuggestions();
        }
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

    public string DurabilityText
    {
        get => _durabilityText;
        set
        {
            _durabilityText = value;
            OnPropertyChanged(nameof(DurabilityText));
            UpdateDurabilitySummary();
        }
    }

    public string MaxDurabilityText
    {
        get => _maxDurabilityText;
        set
        {
            _maxDurabilityText = value;
            OnPropertyChanged(nameof(MaxDurabilityText));
            UpdateDurabilitySummary();
        }
    }

    public string DurabilityPercentText
    {
        get => _durabilityPercentText;
        private set { _durabilityPercentText = value; OnPropertyChanged(nameof(DurabilityPercentText)); }
    }

    public string NameAvailabilityText
    {
        get => _nameAvailabilityText;
        private set { _nameAvailabilityText = value; OnPropertyChanged(nameof(NameAvailabilityText)); }
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

    public string SelectedDurabilityRange
    {
        get => _selectedDurabilityRange;
        set
        {
            if (_selectedDurabilityRange == value) return;
            _selectedDurabilityRange = value;
            OnPropertyChanged(nameof(SelectedDurabilityRange));
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
    public bool IsNameSuggestionVisible
    {
        get => _isNameSuggestionVisible;
        private set
        {
            if (_isNameSuggestionVisible == value)
            {
                return;
            }

            _isNameSuggestionVisible = value;
            OnPropertyChanged(nameof(IsNameSuggestionVisible));
        }
    }

    public string SelectedSortField { get; set; }
    public string SelectedSortOrder { get; set; }
    private bool _isNameSuggestionVisible;

    private void LoadItems()
    {
        var loaded = _repository.Load();
        foreach (var item in loaded)
        {
            _items.Add(item);
        }
        StatusText = _items.Count > 0 ? $"Loaded {_items.Count} items from items.json" : "Ready";
    }

    private void LoadNameSuggestions()
    {
        if (!Directory.Exists(_repository.DefaultImageFolder))
        {
            return;
        }

        _suggestionToImagePath.Clear();

        var formatted = Directory
            .EnumerateFiles(_repository.DefaultImageFolder)
            .Select(file => new
            {
                FilePath = file,
                Name = Path.GetFileNameWithoutExtension(file)
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .Select(entry => new
            {
                entry.FilePath,
                FormattedName = FormatSuggestionName(entry.Name)
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.FormattedName))
            .ToList();

        foreach (var entry in formatted)
        {
            if (!_suggestionToImagePath.ContainsKey(entry.FormattedName))
            {
                _suggestionToImagePath[entry.FormattedName] = entry.FilePath;
            }
        }

        var formattedNames = _suggestionToImagePath
            .Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _allNameSuggestions.Clear();
        _allNameSuggestions.AddRange(formattedNames);
        RefreshNameSuggestions();
    }

    private bool FilterItem(object obj)
    {
        if (obj is not Item item) return false;

        if (!string.IsNullOrWhiteSpace(SearchText) &&
            !item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return MatchesDurabilityFilter(item);
    }

    private bool MatchesDurabilityFilter(Item item)
    {
        if (SelectedDurabilityRange == "All")
        {
            return true;
        }

        if (!TryGetDurabilityPercentage(item.Name, out var durability))
        {
            return true;
        }

        return SelectedDurabilityRange switch
        {
            "1-25%" => durability is >= 1 and <= 25,
            "26-50%" => durability is >= 26 and <= 50,
            "51-75%" => durability is >= 51 and <= 75,
            "76-100%" => durability is >= 76 and <= 100,
            "100%" => durability == 100,
            _ => true,
        };
    }

    private bool TryGetDurabilityPercentage(string name, out int percentage)
    {
        var match = Regex.Match(name, @"(\d+)%");
        if (match.Success && int.TryParse(match.Groups[1].Value, out percentage))
        {
            return true;
        }

        percentage = 0;
        return false;
    }

    private string RemoveDurabilitySuffix(string name)
    {
        var cleaned = Regex.Replace(name, @"\s+\d+%$", string.Empty);
        return cleaned.TrimEnd();
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
        DurabilityText = SelectedItem.Durability?.ToString() ?? string.Empty;
        MaxDurabilityText = SelectedItem.MaxDurability?.ToString() ?? string.Empty;
        _currentIconPath = SelectedItem.IconPath ?? string.Empty;
        IconLabel = string.IsNullOrWhiteSpace(_currentIconPath) ? "No icon selected" : Path.GetFileName(_currentIconPath);
        StatusText = $"Editing item: {SelectedItem.Name}";
        OnPropertyChanged(nameof(AddOrUpdateButtonText));
        IsNameSuggestionVisible = false;
    }

    private void UpdateDurabilitySummary()
    {
        if (TryCalculateRoundedDurability(out var percentageText, out var roundedPercentage))
        {
            DurabilityPercentText = percentageText;
            UpdateNameAvailability(roundedPercentage);
        }
        else
        {
            DurabilityPercentText = string.Empty;
            NameAvailabilityText = string.Empty;
        }
    }

    private bool TryCalculateRoundedDurability(out string percentageText, out int roundedPercentage)
    {
        percentageText = string.Empty;
        roundedPercentage = 0;

        if (int.TryParse(DurabilityText, out var durability) &&
            int.TryParse(MaxDurabilityText, out var maxDurability) &&
            maxDurability > 0 &&
            durability >= 0)
        {
            var percentage = Math.Clamp(durability / (double)maxDurability * 100.0, 0, 100);
            var rounded = Math.Clamp(Math.Round(percentage / 5.0, MidpointRounding.AwayFromZero) * 5, 0, 100);
            var roundedText = $"{rounded:0}%";
            var suffix = Math.Abs(rounded - percentage) > 0.001 ? $" (from {percentage:0.0}%)" : string.Empty;
            percentageText = $"{roundedText}{suffix}";
            roundedPercentage = (int)rounded;
            return true;
        }

        return false;
    }

    private void UpdateNameAvailability(int roundedPercentage)
    {
        var baseName = NameText.Trim();
        var cleanedName = RemoveDurabilitySuffix(baseName);
        var finalName = string.IsNullOrWhiteSpace(cleanedName)
            ? $"{roundedPercentage}%"
            : $"{cleanedName} {roundedPercentage}%";

        var duplicateExists = _items.Any(item =>
            !ReferenceEquals(item, SelectedItem) &&
            item.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase));

        NameAvailabilityText = duplicateExists
            ? $"Name already exists: {finalName}"
            : $"Final name: {finalName} (available)";
    }

    private static string FormatSuggestionName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        string normalized;

        if (rawName.Contains('_'))
        {
            var underscoreCount = rawName.Count(ch => ch == '_');
            if (underscoreCount == 1 && rawName.EndsWith("_", StringComparison.Ordinal))
            {
                normalized = rawName[..^1];
            }
            else
            {
                normalized = rawName.Replace('_', ' ');
            }
        }
        else
        {
            normalized = Regex.Replace(rawName, "(?<!^)(?:(?<=[a-z])(?=[A-Z])|(?<=[A-Za-z])(?=\\d)|(?<=\\d)(?=[A-Za-z]))", " ");
        }

        normalized = Regex.Replace(normalized, "\\s+", " ").Trim();

        var levelMatch = Regex.Match(normalized, @"^Lv\.?\s*(\d+)(.*)", RegexOptions.IgnoreCase);
        if (levelMatch.Success)
        {
            var suffix = levelMatch.Groups[2].Value.TrimStart();
            var formattedPrefix = $"Lv.{levelMatch.Groups[1].Value}";
            return string.IsNullOrWhiteSpace(suffix)
                ? formattedPrefix
                : $"{formattedPrefix} {suffix}";
        }

        var prefixes = new[] { "Totem", "Blueprint", "Recipe" };
        foreach (var prefix in prefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var suffix = normalized[prefix.Length..].TrimStart();
                return string.IsNullOrWhiteSpace(suffix)
                    ? $"{prefix}:"
                    : $"{prefix}: {suffix}";
            }
        }

        return normalized;
    }

    private void RefreshNameSuggestions()
    {
        var previousSelection = NameSuggestionsListBox.SelectedItem as string;
        _filteredNameSuggestions.Clear();

        if (string.IsNullOrWhiteSpace(NameText))
        {
            IsNameSuggestionVisible = false;
            NameSuggestionsListBox.SelectedIndex = -1;
            return;
        }

        var matches = _allNameSuggestions
            .Where(name => name.Contains(NameText, StringComparison.OrdinalIgnoreCase))
            .Take(12)
            .ToList();

        foreach (var match in matches)
        {
            _filteredNameSuggestions.Add(match);
        }

        var hasSuggestions = _filteredNameSuggestions.Count > 0;
        IsNameSuggestionVisible = hasSuggestions;

        if (!hasSuggestions)
        {
            NameSuggestionsListBox.SelectedIndex = -1;
            return;
        }

        var previouslySelectedIndex = previousSelection is not null
            ? _filteredNameSuggestions.IndexOf(previousSelection)
            : -1;

        NameSuggestionsListBox.SelectedIndex = previouslySelectedIndex >= 0
            ? previouslySelectedIndex
            : 0;

        NameSuggestionsListBox.ScrollIntoView(NameSuggestionsListBox.SelectedItem);
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

        if (!TryParseDurability(out var durability, out var maxDurability, out var priceAdjustmentPercent, out var roundedDurability))
        {
            return false;
        }

        if (Math.Abs(priceAdjustmentPercent) > 0.001)
        {
            unitPrice *= 1 + priceAdjustmentPercent / 100.0;
        }

        var baseName = NameText.Trim();

        if (roundedDurability is int rounded)
        {
            baseName = RemoveDurabilitySuffix(baseName);
            baseName = string.IsNullOrWhiteSpace(baseName) ? $"{rounded}%" : $"{baseName} {rounded}%";
        }

        item = new Item
        {
            Name = baseName,
            UnitPrice = unitPrice,
            StackSize = stackSize,
            WeightPerItem = weight,
            Durability = durability,
            MaxDurability = maxDurability,
            IconPath = _currentIconPath,
        };

        return true;
    }

    private bool TryParseDurability(out int? durability, out int? maxDurability, out double priceAdjustmentPercent, out int? roundedDurability)
    {
        durability = null;
        maxDurability = null;
        priceAdjustmentPercent = 0;
        roundedDurability = null;

        var hasDurability = !string.IsNullOrWhiteSpace(DurabilityText);
        var hasMax = !string.IsNullOrWhiteSpace(MaxDurabilityText);

        if (!hasDurability && !hasMax)
        {
            return true;
        }

        if (!hasDurability || !hasMax)
        {
            MessageBox.Show("Please enter both durability and max durability, or leave both blank.", "Incomplete durability", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(DurabilityText, out var durabilityValue) || !int.TryParse(MaxDurabilityText, out var maxDurabilityValue))
        {
            MessageBox.Show("Please enter whole numbers for durability and max durability.", "Invalid durability", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        if (durabilityValue < 0 || maxDurabilityValue <= 0)
        {
            MessageBox.Show("Durability must be zero or higher and max durability must be greater than zero.", "Invalid durability range", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        durability = durabilityValue;
        maxDurability = maxDurabilityValue;

        var percentage = Math.Clamp(durabilityValue / (double)maxDurabilityValue * 100.0, 0, 100);
        roundedDurability = (int)Math.Clamp(Math.Round(percentage / 5.0, MidpointRounding.AwayFromZero) * 5, 0, 100);

        var roundingDifference = roundedDurability.Value - percentage;
        priceAdjustmentPercent = roundingDifference * 2.5;

        return true;
    }

    private void OnAddOrUpdateCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        OnAddOrUpdate(sender, e);
        e.Handled = true;
    }

    private void OnAddOrUpdateCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = true;
        e.Handled = true;
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
        DurabilityText = string.Empty;
        MaxDurabilityText = string.Empty;
        _currentIconPath = string.Empty;
        IconLabel = "No icon selected";
        NameAvailabilityText = string.Empty;
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

    private void OnNameTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsNameSuggestionVisible || _filteredNameSuggestions.Count == 0)
        {
            return;
        }

        if (e.Key == Key.Down)
        {
            NameSuggestionsListBox.Focus();
            if (NameSuggestionsListBox.SelectedIndex <= 0)
            {
                NameSuggestionsListBox.SelectedIndex = 0;
            }
            else if (NameSuggestionsListBox.SelectedIndex < NameSuggestionsListBox.Items.Count - 1)
            {
                NameSuggestionsListBox.SelectedIndex++;
            }
            NameSuggestionsListBox.ScrollIntoView(NameSuggestionsListBox.SelectedItem);
            e.Handled = true;
        }
    }

    private void OnNameSuggestionKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            var lastIndex = NameSuggestionsListBox.Items.Count - 1;
            if (NameSuggestionsListBox.SelectedIndex < lastIndex)
            {
                NameSuggestionsListBox.SelectedIndex++;
            }
            NameSuggestionsListBox.ScrollIntoView(NameSuggestionsListBox.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (NameSuggestionsListBox.SelectedIndex > 0)
            {
                NameSuggestionsListBox.SelectedIndex--;
                NameSuggestionsListBox.ScrollIntoView(NameSuggestionsListBox.SelectedItem);
            }
            else
            {
                NameTextBox.Focus();
                NameTextBox.CaretIndex = NameText.Length;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && NameSuggestionsListBox.SelectedItem is string suggestion)
        {
            ApplyNameSuggestion(suggestion);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            IsNameSuggestionVisible = false;
            NameTextBox.Focus();
            e.Handled = true;
        }
    }

    private void OnNameSuggestionClicked(object sender, MouseButtonEventArgs e)
    {
        if (NameSuggestionsListBox.SelectedItem is string suggestion)
        {
            ApplyNameSuggestion(suggestion);
            e.Handled = true;
        }
    }

    private void ApplyNameSuggestion(string suggestion)
    {
        NameText = suggestion;
        IsNameSuggestionVisible = false;
        ApplyIconForSuggestion(suggestion);
        ApplyExistingItemDetailsIfAvailable(suggestion);
        NameTextBox.CaretIndex = NameText.Length;
        NameTextBox.Focus();
    }

    private void ApplyIconForSuggestion(string suggestion)
    {
        if (_suggestionToImagePath.TryGetValue(suggestion, out var imagePath) && File.Exists(imagePath))
        {
            _currentIconPath = imagePath;
            IconLabel = Path.GetFileName(imagePath);
        }
    }

    private void ApplyExistingItemDetailsIfAvailable(string suggestion)
    {
        Item? matchingItem = FindItemByIconPath();

        if (matchingItem is null)
        {
            var normalizedSuggestion = FormatSuggestionName(suggestion);

            matchingItem = _items
                .Select(item => new
                {
                    Item = item,
                    Normalized = FormatSuggestionName(RemoveDurabilitySuffix(item.Name)),
                    DurabilityScore = GetDurabilityScore(item)
                })
                .Where(entry => entry.Normalized.Equals(normalizedSuggestion, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(entry => entry.DurabilityScore)
                .Select(entry => entry.Item)
                .FirstOrDefault();
        }

        if (matchingItem is null)
        {
            return;
        }

        StackSizeText = matchingItem.StackSize.ToString();
        WeightText = matchingItem.WeightPerItem.ToString("F3");
        MaxDurabilityText = matchingItem.MaxDurability?.ToString() ?? string.Empty;
        _currentIconPath = matchingItem.IconPath ?? string.Empty;
        IconLabel = string.IsNullOrWhiteSpace(_currentIconPath)
            ? "No icon selected"
            : Path.GetFileName(_currentIconPath);
    }

    private Item? FindItemByIconPath()
    {
        if (string.IsNullOrWhiteSpace(_currentIconPath))
        {
            return null;
        }

        var currentFileName = Path.GetFileName(_currentIconPath);
        if (string.IsNullOrWhiteSpace(currentFileName))
        {
            return null;
        }

        return _items.FirstOrDefault(item =>
        {
            if (string.IsNullOrWhiteSpace(item.IconPath))
            {
                return false;
            }

            var itemFileName = Path.GetFileName(item.IconPath);
            return currentFileName.Equals(itemFileName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private double GetDurabilityScore(Item item)
    {
        if (item.RoundedDurabilityPercentage is double rounded)
        {
            return rounded;
        }

        if (TryGetDurabilityPercentage(item.Name, out var parsed))
        {
            return parsed;
        }

        return 100;
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
        _repository.CopyDataFile();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
