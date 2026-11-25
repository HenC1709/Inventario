using System.Globalization;
using Microsoft.Data.Sqlite;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Enums;
using TiendaBodegas.Domain.Interfaces;

namespace TiendaBodegas.Infrastructure.Persistence.Sqlite.Repositories;

/// <summary>
/// SQLite-backed repository implementation for <see cref="Supplier"/> entities.
/// </summary>
public class SqliteSupplierRepository : ISupplierRepository
{
    private readonly SqliteDataContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteSupplierRepository"/>.
    /// </summary>
    public SqliteSupplierRepository(SqliteDataContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public IEnumerable<Supplier> GetAll()
    {
        var suppliers = new List<Supplier>();
        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Code, Name, Contact, MainCategory, Status
            FROM Suppliers
            ORDER BY Id;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            suppliers.Add(MapSupplier(reader));
        }

        return suppliers;
    }

    /// <inheritdoc />
    public Supplier? GetById(int id)
    {
        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Code, Name, Contact, MainCategory, Status
            FROM Suppliers
            WHERE Id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapSupplier(reader) : null;
    }

    /// <inheritdoc />
    public Supplier? GetByCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return null;

        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Code, Name, Contact, MainCategory, Status
            FROM Suppliers
            WHERE Code = @Code COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("@Code", code);

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapSupplier(reader) : null;
    }

    /// <inheritdoc />
    public Supplier? GetByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Code, Name, Contact, MainCategory, Status
            FROM Suppliers
            WHERE Name = @Name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("@Name", name);

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapSupplier(reader) : null;
    }

    /// <inheritdoc />
    public void Add(Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        using var connection = _context.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        var needsGeneratedCode = string.IsNullOrWhiteSpace(supplier.Code);
        var initialCode = needsGeneratedCode ? $"__TMP_{Guid.NewGuid():N}" : supplier.Code;

        int newId;
        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO Suppliers (Code, Name, Contact, MainCategory, Status)
                VALUES (@Code, @Name, @Contact, @MainCategory, @Status);
                SELECT last_insert_rowid();
                """;
            insertCommand.Parameters.AddWithValue("@Code", initialCode);
            insertCommand.Parameters.AddWithValue("@Name", supplier.Name);
            insertCommand.Parameters.AddWithValue("@Contact", supplier.Contact);
            insertCommand.Parameters.AddWithValue("@MainCategory", supplier.MainCategory);
            insertCommand.Parameters.AddWithValue("@Status", (int)supplier.Status);
            newId = Convert.ToInt32(insertCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        supplier.AssignId(newId);

        if (needsGeneratedCode)
        {
            using var updateCodeCommand = connection.CreateCommand();
            updateCodeCommand.Transaction = transaction;
            updateCodeCommand.CommandText = "UPDATE Suppliers SET Code = @Code WHERE Id = @Id;";
            updateCodeCommand.Parameters.AddWithValue("@Code", supplier.Code);
            updateCodeCommand.Parameters.AddWithValue("@Id", newId);
            updateCodeCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <inheritdoc />
    public void Update(Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Suppliers
            SET Code = @Code,
                Name = @Name,
                Contact = @Contact,
                MainCategory = @MainCategory,
                Status = @Status
            WHERE Id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", supplier.Id);
        command.Parameters.AddWithValue("@Code", supplier.Code);
        command.Parameters.AddWithValue("@Name", supplier.Name);
        command.Parameters.AddWithValue("@Contact", supplier.Contact);
        command.Parameters.AddWithValue("@MainCategory", supplier.MainCategory);
        command.Parameters.AddWithValue("@Status", (int)supplier.Status);
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public void Delete(int id)
    {
        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Suppliers WHERE Id = @Id;";
        command.Parameters.AddWithValue("@Id", id);
        command.ExecuteNonQuery();
    }

    private static Supplier MapSupplier(SqliteDataReader reader)
    {
        var id = reader.GetInt32(0);
        var code = reader.GetString(1);
        var name = reader.GetString(2);
        var contact = reader.GetString(3);
        var mainCategory = reader.GetString(4);
        var status = (SupplierStatus)reader.GetInt32(5);

        var supplier = new Supplier(name, contact, mainCategory, status, code);
        supplier.AssignId(id);
        return supplier;
    }
}
