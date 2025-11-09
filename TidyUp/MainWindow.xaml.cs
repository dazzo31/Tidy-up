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
}
