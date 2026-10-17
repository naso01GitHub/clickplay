using Avalonia.Controls;
using ClickPlay.App.ViewModels;

namespace ClickPlay.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.LoadSportsCommand.ExecuteAsync(null);
        }
    }
}