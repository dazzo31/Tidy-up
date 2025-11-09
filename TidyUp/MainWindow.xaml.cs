using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private Rule? _draggedRule;
    private Point _startPoint;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        // Set window to fill screen except taskbar
        Loaded += (s, e) =>
        {
            MaximizeToWorkArea();
        };
        
        // Load rules on startup
        Loaded += async (s, e) => await viewModel.LoadRulesCommand.ExecuteAsync(null);
    }

    private void MaximizeToWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left;
        Top = workArea.Top;
        Width = workArea.Width;
        Height = workArea.Height;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to maximize/restore
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            // Single click to drag
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
        
        // Find the ListBoxItem that was clicked
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

            // Only start drag if moved enough
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

            // Find the target rule (where we're dropping)
            var dropTarget = e.OriginalSource as FrameworkElement;
            var targetListBoxItem = FindAncestor<ListBoxItem>(dropTarget);
            
            if (targetListBoxItem != null && targetListBoxItem.DataContext is Rule targetRule)
            {
                if (sourceRule != targetRule)
                {
                    // Reorder in the Rules collection
                    int oldIndex = ViewModel.Rules.IndexOf(sourceRule);
                    int newIndex = ViewModel.Rules.IndexOf(targetRule);

                    if (oldIndex != -1 && newIndex != -1)
                    {
                        ViewModel.Rules.Move(oldIndex, newIndex);
                        
                        // Update ExecutionOrder for all rules
                        for (int i = 0; i < ViewModel.Rules.Count; i++)
                        {
                            ViewModel.Rules[i].ExecutionOrder = i;
                        }

                        // Refresh filtered view
                        ViewModel.SearchText = ViewModel.SearchText; // Trigger filter
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
