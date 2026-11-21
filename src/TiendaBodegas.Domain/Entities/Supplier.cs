using System.Text.Json.Serialization;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Domain.Entities;

/// <summary>
/// Represents an inventory supplier with contact details, primary category, and review status.
/// </summary>
public class Supplier
{
    [JsonInclude]
    public int Id { get; private set; }

    [JsonInclude]
    [JsonPropertyName("Codigo")]
    public string Code { get; private set; } = string.Empty;

    [JsonInclude]
    [JsonPropertyName("Nombre")]
    public string Name { get; private set; } = string.Empty;

    [JsonInclude]
    [JsonPropertyName("Contacto")]
    public string Contact { get; private set; } = string.Empty;

    [JsonInclude]
    [JsonPropertyName("CategoriaPrincipal")]
    public string MainCategory { get; private set; } = string.Empty;

    [JsonInclude]
    [JsonPropertyName("Estado")]
    public SupplierStatus Status { get; private set; } = SupplierStatus.Active;

    /// <summary>
    /// Parameterless constructor required for JSON deserialization.
    /// </summary>
    public Supplier() { }

    /// <summary>
    /// Initializes a new supplier with its business name, contact, main category, optional status, and optional code.
    /// </summary>
    public Supplier(
        string name,
        string contact,
        string mainCategory,
        SupplierStatus status = SupplierStatus.Active,
        string code = "")
    {
        Name = name.Trim();
        Contact = contact.Trim();
        MainCategory = mainCategory.Trim();
        Status = status;
        Code = code.Trim();
    }

    /// <summary>
    /// Assigns a unique identifier to the supplier and generates a default <c>PRV-XX</c> code when none is set.
    /// </summary>
    public void AssignId(int id)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Id must be greater than zero.");

        if (Id == 0)
            Id = id;

        if (string.IsNullOrWhiteSpace(Code))
            Code = $"PRV-{id:D2}";
    }

    /// <summary>
    /// Updates the editable supplier fields while preserving its identity.
    /// </summary>
    public void Update(
        string name,
        string contact,
        string mainCategory,
        SupplierStatus status,
        string? code = null)
    {
        Name = name.Trim();
        Contact = contact.Trim();
        MainCategory = mainCategory.Trim();
        Status = status;

        if (!string.IsNullOrWhiteSpace(code))
            Code = code.Trim();
    }
}
