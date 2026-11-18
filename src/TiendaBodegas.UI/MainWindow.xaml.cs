using System.Windows;
using TiendaBodegas.UI.ViewModels;

namespace TiendaBodegas.UI;

/// <summary>
/// Main application shell hosting sidebar navigation and the active section view.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of <see cref="MainWindow"/> with its bound <see cref="MainViewModel"/>.
    /// </summary>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
