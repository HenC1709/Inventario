using CommunityToolkit.Mvvm.ComponentModel;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel for the application settings section (full preferences form implemented in R9).
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    /// <summary>
    /// Gets the page title displayed in the settings header.
    /// </summary>
    public string Title => "Ajustes del Sistema";

    /// <summary>
    /// Gets the subtitle describing the settings section.
    /// </summary>
    public string Subtitle => "Preferencias de la aplicación de escritorio";
}
