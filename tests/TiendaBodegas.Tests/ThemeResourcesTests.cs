using System.IO.Packaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TiendaBodegas.UI;
using TiendaBodegas.UI.ViewModels;
using Xunit;

namespace TiendaBodegas.Tests;

/// <summary>
/// Unit tests verifying that global WPF design tokens and control styles load cleanly via Pack URIs.
/// </summary>
public class ThemeResourcesTests
{
    /// <summary>
    /// Verifies that <c>Themes/Colors.xaml</c> and <c>Themes/Controls.xaml</c> load without missing keys on an STA thread.
    /// </summary>
    [Fact]
    public void ThemeDictionaries_LoadOnStaThread_WithExpectedTokensAndStyles()
    {
        Exception? threadException = null;

        var staThread = new Thread(() =>
        {
            try
            {
                if (!UriParser.IsKnownScheme("pack"))
                    _ = PackUriHelper.UriSchemePack;

                System.Windows.Application.ResourceAssembly ??= typeof(App).Assembly;

                var colors = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/TiendaBodegas.UI;component/Themes/Colors.xaml", UriKind.Absolute)
                };

                var controls = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/TiendaBodegas.UI;component/Themes/Controls.xaml", UriKind.Absolute)
                };

                var pearlBrush = Assert.IsType<SolidColorBrush>(colors["Pearl100Brush"]);
                Assert.Equal(Color.FromRgb(0xF9, 0xF7, 0xF4), pearlBrush.Color);

                var warmBrush = Assert.IsType<SolidColorBrush>(colors["Warm500Brush"]);
                Assert.Equal(Color.FromRgb(0xC8, 0x6D, 0x44), warmBrush.Color);

                var coolBrush = Assert.IsType<SolidColorBrush>(colors["Cool600Brush"]);
                Assert.Equal(Color.FromRgb(0x1E, 0x4E, 0x79), coolBrush.Color);

                Assert.IsType<LinearGradientBrush>(colors["AppBackgroundBrush"]);
                Assert.IsType<LinearGradientBrush>(colors["SidebarBackgroundBrush"]);
                Assert.NotNull(colors["SuccessBgBrush"]);
                Assert.NotNull(colors["WarningBgBrush"]);
                Assert.NotNull(colors["DangerBgBrush"]);

                Assert.IsType<Style>(controls["PrimaryButtonStyle"]);
                Assert.IsType<Style>(controls["SecondaryCoolButtonStyle"]);
                Assert.IsType<Style>(controls["TableIconButtonStyle"]);
                Assert.IsType<Style>(controls["SidebarNavButtonStyle"]);
                Assert.IsType<Style>(controls["ModernTextBoxStyle"]);
                Assert.IsType<Style>(controls["ModernComboBoxStyle"]);
                Assert.IsType<Style>(controls["ModernDataGridStyle"]);
                Assert.IsType<Style>(controls["KpiPrimaryFocusCardStyle"]);
                Assert.IsType<Style>(controls["KpiAlertCardStyle"]);
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        if (threadException != null)
            throw threadException;
    }

    /// <summary>
    /// Verifies that <c>ModernComboBoxStyle</c> wires <see cref="ItemsControl.ItemTemplateSelector"/> so <c>DisplayMemberPath</c> renders properly.
    /// </summary>
    [Fact]
    public void ModernComboBoxStyle_WiresItemTemplateSelectorForDisplayMemberPath()
    {
        Exception? threadException = null;

        var staThread = new Thread(() =>
        {
            try
            {
                if (!UriParser.IsKnownScheme("pack"))
                    _ = PackUriHelper.UriSchemePack;

                System.Windows.Application.ResourceAssembly ??= typeof(App).Assembly;

                var controls = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/TiendaBodegas.UI;component/Themes/Controls.xaml", UriKind.Absolute)
                };

                var comboBox = new ComboBox
                {
                    Style = (Style)controls["ModernComboBoxStyle"],
                    DisplayMemberPath = "Label",
                    ItemsSource = new[] { new MovementTypeFilterOption(null, "Todos") },
                    SelectedIndex = 0
                };

                Assert.True(comboBox.ApplyTemplate());
                var contentSite = Assert.IsType<ContentPresenter>(comboBox.Template.FindName("ContentSite", comboBox));
                Assert.NotNull(comboBox.ItemTemplateSelector);
                Assert.Same(comboBox.ItemTemplateSelector, contentSite.ContentTemplateSelector);
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        if (threadException != null)
            throw threadException;
    }
}
