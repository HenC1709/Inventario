using System.Windows.Controls;

namespace TiendaBodegas.UI.Views;

/// <summary>
/// View displaying stock levels per warehouse, global product totals, and low-stock indicators.
/// </summary>
public partial class StockView : UserControl
{
    /// <summary>
    /// Initializes a new instance of <see cref="StockView"/>.
    /// </summary>
    public StockView()
    {
        InitializeComponent();
    }
}
