using System.Windows;

namespace TiendaBodegas.UI.Views;

/// <summary>
/// Styled confirmation modal window replacing default system message boxes for destructive actions.
/// </summary>
public partial class ConfirmDialogWindow : Window
{
    /// <summary>
    /// Initializes a new instance of <see cref="ConfirmDialogWindow"/> with the given title and message.
    /// </summary>
    public ConfirmDialogWindow(string title, string message)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
