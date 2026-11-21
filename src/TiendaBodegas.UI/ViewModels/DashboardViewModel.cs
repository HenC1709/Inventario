using CommunityToolkit.Mvvm.ComponentModel;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel for the executive dashboard section (full widgets implemented in R7).
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    /// <summary>
    /// Gets the page title displayed in the dashboard header.
    /// </summary>
    public string Title => "Dashboard General";

    /// <summary>
    /// Gets the subtitle describing the dashboard section.
    /// </summary>
    public string Subtitle => "Resumen ejecutivo del estado del almacén";
}
