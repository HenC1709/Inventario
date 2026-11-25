using System.Globalization;
using Microsoft.Data.Sqlite;
using TiendaBodegas.Domain.Entities;
using TiendaBodegas.Domain.Interfaces;

namespace TiendaBodegas.Infrastructure.Persistence.Sqlite.Repositories;

/// <summary>
/// SQLite-backed repository implementation for <see cref="Product"/> entities.
/// </summary>
public class SqliteProductRepository : IProductRepository
{
    private readonly SqliteDataContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteProductRepository"/>.
    /// </summary>
    public SqliteProductRepository(SqliteDataContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public IEnumerable<Product> GetAll()
    {
        var products = new List<Product>();
        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, SKU, Category, SupplierId, PurchasePrice, SalePrice, MinStock
            FROM Products
            ORDER BY Id;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            products.Add(MapProduct(reader));
        }

        return products;
    }

    /// <inheritdoc />
    public Product? GetById(int id)
    {
        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, SKU, Category, SupplierId, PurchasePrice, SalePrice, MinStock
            FROM Products
            WHERE Id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapProduct(reader) : null;
    }

    /// <inheritdoc />
    public Product? GetBySku(string sku)
    {
        if (string.IsNullOrEmpty(sku))
            return null;

        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, SKU, Category, SupplierId, PurchasePrice, SalePrice, MinStock
            FROM Products
            WHERE SKU = @Sku COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("@Sku", sku);

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapProduct(reader) : null;
    }

    /// <inheritdoc />
    public Product? GetByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, SKU, Category, SupplierId, PurchasePrice, SalePrice, MinStock
            FROM Products
            WHERE Name = @Name;
            """;
        command.Parameters.AddWithValue("@Name", name);

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapProduct(reader) : null;
    }

    /// <inheritdoc />
    public IEnumerable<Product> GetBySupplierId(int supplierId)
    {
        var products = new List<Product>();
        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, SKU, Category, SupplierId, PurchasePrice, SalePrice, MinStock
            FROM Products
            WHERE SupplierId = @SupplierId
            ORDER BY Id;
            """;
        command.Parameters.AddWithValue("@SupplierId", supplierId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            products.Add(MapProduct(reader));
        }

        return products;
    }

    /// <inheritdoc />
    public void Add(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Products (Name, SKU, Category, SupplierId, PurchasePrice, SalePrice, MinStock)
            VALUES (@Name, @Sku, @Category, @SupplierId, @PurchasePrice, @SalePrice, @MinStock);
            SELECT last_insert_rowid();
            """;
        BindProductParameters(command, product);

        var newId = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        product.AssignId(newId);
    }

    /// <inheritdoc />
    public void Update(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Products
            SET Name = @Name,
                SKU = @Sku,
                Category = @Category,
                SupplierId = @SupplierId,
                PurchasePrice = @PurchasePrice,
                SalePrice = @SalePrice,
                MinStock = @MinStock
            WHERE Id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", product.Id);
        BindProductParameters(command, product);
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public void UpdateRange(IEnumerable<Product> products)
    {
        ArgumentNullException.ThrowIfNull(products);

        var list = products.ToList();
        if (list.Count == 0)
            return;

        using var connection = _context.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var product in list)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Products
                SET Name = @Name,
                    SKU = @Sku,
                    Category = @Category,
                    SupplierId = @SupplierId,
                    PurchasePrice = @PurchasePrice,
                    SalePrice = @SalePrice,
                    MinStock = @MinStock
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", product.Id);
            BindProductParameters(command, product);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <inheritdoc />
    public void Delete(int id)
    {
        using var connection = _context.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Products WHERE Id = @Id;";
        command.Parameters.AddWithValue("@Id", id);
        command.ExecuteNonQuery();
    }

    private static void BindProductParameters(SqliteCommand command, Product product)
    {
        command.Parameters.AddWithValue("@Name", product.Name);
        command.Parameters.AddWithValue("@Sku", product.SKU);
        command.Parameters.AddWithValue("@Category", product.Category);
        command.Parameters.AddWithValue("@SupplierId", product.SupplierId.HasValue ? product.SupplierId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@PurchasePrice", product.PurchasePrice.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@SalePrice", product.SalePrice.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@MinStock", product.MinStock);
    }

    private static Product MapProduct(SqliteDataReader reader)
    {
        var id = reader.GetInt32(0);
        var name = reader.GetString(1);
        var sku = reader.GetString(2);
        var category = reader.GetString(3);
        int? supplierId = reader.IsDBNull(4) ? null : reader.GetInt32(4);
        var purchasePrice = decimal.Parse(reader.GetString(5), CultureInfo.InvariantCulture);
        var salePrice = decimal.Parse(reader.GetString(6), CultureInfo.InvariantCulture);
        var minStock = reader.GetInt32(7);

        var product = new Product(name, sku, category, supplierId, purchasePrice, salePrice, minStock);
        product.AssignId(id);
        return product;
    }
}
