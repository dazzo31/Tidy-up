using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TidyUp.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        DataContext = new HelpViewModel();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public partial class HelpViewModel : ObservableObject
{
    [ObservableProperty]
    private string _gettingStartedContent = string.Empty;

    [ObservableProperty]
    private string _variablesContent = string.Empty;

    [ObservableProperty]
    private string _examplesContent = string.Empty;

    [ObservableProperty]
    private string _faqContent = string.Empty;

    public HelpViewModel()
    {
        LoadHelpContent();
    }

    private void LoadHelpContent()
    {
        try
        {
            var helpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Help");

            GettingStartedContent = LoadMarkdownFile(Path.Combine(helpPath, "getting-started.md"));
            VariablesContent = LoadMarkdownFile(Path.Combine(helpPath, "variables-reference.md"));
            ExamplesContent = LoadMarkdownFile(Path.Combine(helpPath, "examples.md"));
            FaqContent = LoadMarkdownFile(Path.Combine(helpPath, "faq.md"));
        }
        catch (Exception ex)
        {
            GettingStartedContent = $"# Error Loading Help\n\nCould not load help files: {ex.Message}";
            VariablesContent = GettingStartedContent;
            ExamplesContent = GettingStartedContent;
            FaqContent = GettingStartedContent;
        }
    }

    private string LoadMarkdownFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }
        return $"# File Not Found\n\nCould not find help file: {filePath}";
    }
}
