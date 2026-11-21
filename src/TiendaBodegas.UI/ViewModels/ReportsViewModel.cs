using CommunityToolkit.Mvvm.ComponentModel;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel for the reports and exports section (full widgets implemented in R8).
/// </summary>
public partial class ReportsViewModel : ObservableObject
{
    /// <summary>
    /// Gets the page title displayed in the reports header.
    /// </summary>
    public string Title => "Reportes";

    /// <summary>
    /// Gets the subtitle describing the reports section.
    /// </summary>
    public string Subtitle => "Generación de documentos y auditorías de stock";
}
