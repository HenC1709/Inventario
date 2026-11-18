using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel presenting the read-only list of persisted warehouses.
/// </summary>
public partial class WarehousesViewModel : ObservableObject
{
    private readonly IWarehouseRepository _warehouseRepository;

    /// <summary>
    /// Gets the observable collection of warehouses bound to the UI.
    /// </summary>
    public ObservableCollection<Warehouse> Warehouses { get; } = new();

    /// <summary>
    /// Initializes a new instance of <see cref="WarehousesViewModel"/> and loads initial data.
    /// </summary>
    public WarehousesViewModel(IWarehouseRepository warehouseRepository)
    {
        _warehouseRepository = warehouseRepository;
        LoadData();
    }

    /// <summary>
    /// Reloads all warehouses from the repository into <see cref="Warehouses"/>.
    /// </summary>
    public void LoadData()
    {
        Warehouses.Clear();
        foreach (var warehouse in _warehouseRepository.GetAll())
        {
            Warehouses.Add(warehouse);
        }
    }
}
