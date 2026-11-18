using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel presenting the read-only catalog of persisted products.
/// </summary>
public partial class ProductsViewModel : ObservableObject
{
    private readonly IProductRepository _productRepository;

    /// <summary>
    /// Gets the observable collection of products bound to the UI.
    /// </summary>
    public ObservableCollection<Product> Products { get; } = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ProductsViewModel"/> and loads initial data.
    /// </summary>
    public ProductsViewModel(IProductRepository productRepository)
    {
        _productRepository = productRepository;
        LoadData();
    }

    /// <summary>
    /// Reloads all products from the repository into <see cref="Products"/>.
    /// </summary>
    public void LoadData()
    {
        Products.Clear();
        foreach (var product in _productRepository.GetAll())
        {
            Products.Add(product);
        }
    }
}
