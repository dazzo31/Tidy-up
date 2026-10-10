using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TidyUp.Data;
using TidyUp.Models.Domain;
using TidyUp.ViewModels;
using Microsoft.Win32;

namespace TidyUp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;
    private readonly SettingsRepository? _settingsRepository;
    private Rule? _draggedRule;
    private Point _startPoint;

    public MainWindow(MainWindowViewModel viewModel, SettingsRepository? settingsRepository = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settingsRepository = settingsRepository;
        
        RestoreWindowGeometry();

        // Load rules on startup
        Loaded += async (s, e) => await viewModel.LoadRulesCommand.ExecuteAsync(null);
        Closing += OnMainWindowClosing;
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (ViewModel.IsRuleDirty)
        {
            if (!ViewModel.ConfirmDiscardRuleEdits())
            {
                e.Cancel = true;
                return;
            }
        }

        SaveWindowGeometry();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && App.AppSettings?.MinimizeToTray == true)
        {
            Hide();
        }
    }

    private void RestoreWindowGeometry()
    {
        var settings = App.AppSettings;
        if (settings == null) return;

        if (settings.WindowWidth.HasValue && settings.WindowHeight.HasValue)
        {
            Width = Math.Max(MinWidth, settings.WindowWidth.Value);
            Height = Math.Max(MinHeight, settings.WindowHeight.Value);

            if (settings.WindowLeft.HasValue && settings.WindowTop.HasValue)
            {
                double left = settings.WindowLeft.Value;
                double top = settings.WindowTop.Value;

                if (left >= SystemParameters.VirtualScreenLeft &&
                    left + 100 <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
                    top >= SystemParameters.VirtualScreenTop &&
                    top + 100 <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = left;
                    Top = top;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.WindowState) &&
            Enum.TryParse<WindowState>(settings.WindowState, out var state) &&
            state != WindowState.Minimized)
        {
            WindowState = state;
        }
    }

    private void SaveWindowGeometry()
    {
        var settings = App.AppSettings;
        if (settings == null) return;

        if (WindowState == WindowState.Maximized)
        {
            settings.WindowState = "Maximized";
            settings.WindowWidth = RestoreBounds.Width;
            settings.WindowHeight = RestoreBounds.Height;
            settings.WindowLeft = RestoreBounds.Left;
            settings.WindowTop = RestoreBounds.Top;
        }
        else if (WindowState == WindowState.Normal)
        {
            settings.WindowState = "Normal";
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
        }

        if (_settingsRepository != null)
        {
            _ = _settingsRepository.SaveAsync(settings);
        }
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedRule != null)
        {
            ViewModel.SelectedRule.MonitoredFolders.Add(new MonitoredFolder
            {
                Path = "",
                IncludeSubfolders = true
            });
        }
    }

    private void RemoveFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MonitoredFolder folder)
        {
            ViewModel.SelectedRule?.MonitoredFolders.Remove(folder);
        }
    }

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MonitoredFolder folder)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Folder to Monitor",
                InitialDirectory = string.IsNullOrEmpty(folder.Path) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : folder.Path
            };

            if (dialog.ShowDialog() == true)
            {
                folder.Path = dialog.FolderName;
            }
        }
    }

    #region Drag-Drop for Rule Reordering

    private void RuleListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(null);
        
        if (e.OriginalSource is FrameworkElement element)
        {
            var listBoxItem = FindAncestor<ListBoxItem>(element);
            if (listBoxItem != null && listBoxItem.DataContext is Rule rule)
            {
                _draggedRule = rule;
            }
        }
    }

    private void RuleListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _draggedRule != null)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _startPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (sender is ListBox listBox)
                {
                    var data = new DataObject("Rule", _draggedRule);
                    DragDrop.DoDragDrop(listBox, data, DragDropEffects.Move);
                    _draggedRule = null;
                }
            }
        }
    }

    private void RuleListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("Rule"))
        {
            var sourceRule = e.Data.GetData("Rule") as Rule;
            if (sourceRule == null) return;

            var dropTarget = e.OriginalSource as FrameworkElement;
            var targetListBoxItem = FindAncestor<ListBoxItem>(dropTarget);
            
            if (targetListBoxItem != null && targetListBoxItem.DataContext is Rule targetRule)
            {
                if (sourceRule != targetRule)
                {
                    int oldIndex = ViewModel.Rules.IndexOf(sourceRule);
                    int newIndex = ViewModel.Rules.IndexOf(targetRule);

                    if (oldIndex != -1 && newIndex != -1)
                    {
                        ViewModel.Rules.Move(oldIndex, newIndex);
                        
                        for (int i = 0; i < ViewModel.Rules.Count; i++)
                        {
                            ViewModel.Rules[i].ExecutionOrder = i;
                        }

                        ViewModel.SearchText = ViewModel.SearchText;
                    }
                }
            }
        }
        _draggedRule = null;
    }

    private void RuleListBox_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("Rule"))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T ancestor)
            {
                return ancestor;
            }
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    #endregion
}
