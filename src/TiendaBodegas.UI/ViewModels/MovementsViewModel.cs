using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;

namespace TiendaBodegas.UI.ViewModels;

/// <summary>
/// ViewModel presenting the read-only history of persisted stock movements.
/// </summary>
public partial class MovementsViewModel : ObservableObject
{
    private readonly IStockMovementRepository _movementRepository;

    /// <summary>
    /// Gets the observable collection of stock movements bound to the UI.
    /// </summary>
    public ObservableCollection<StockMovement> Movements { get; } = new();

    /// <summary>
    /// Initializes a new instance of <see cref="MovementsViewModel"/> and loads initial data.
    /// </summary>
    public MovementsViewModel(IStockMovementRepository movementRepository)
    {
        _movementRepository = movementRepository;
        LoadData();
    }

    /// <summary>
    /// Reloads all stock movements ordered from newest to oldest into <see cref="Movements"/>.
    /// </summary>
    public void LoadData()
    {
        Movements.Clear();
        foreach (var movement in _movementRepository.GetAll().OrderByDescending(m => m.Date))
        {
            Movements.Add(movement);
        }
    }
}
