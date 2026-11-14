using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Infrastructure.Persistence.Json
{
    public class JsonDataContext
    {
        public List<Warehouse> Warehouses { get; private set; } = new();
        public List<Product> Products { get; private set; } = new();
        public List<StockMovement> StockMovements { get; private set; } = new();

        public void SaveJson()
        {
            var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            Directory.CreateDirectory(dataDirectory);

            var options = new JsonSerializerOptions { WriteIndented = true };

            File.WriteAllText(Path.Combine(dataDirectory, "bodegas.json"), JsonSerializer.Serialize(Warehouses, options));
            File.WriteAllText(Path.Combine(dataDirectory, "productos.json"), JsonSerializer.Serialize(Products, options));
            File.WriteAllText(Path.Combine(dataDirectory, "tickets.json"), JsonSerializer.Serialize(StockMovements, options));
        }

        public void LoadJson()
        {
            var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            Directory.CreateDirectory(dataDirectory);

            var warehousesPath = Path.Combine(dataDirectory, "bodegas.json");
            var productsPath = Path.Combine(dataDirectory, "productos.json");
            var movementsPath = Path.Combine(dataDirectory, "tickets.json");
            if (File.Exists(warehousesPath))
            {
                var json = File.ReadAllText(warehousesPath);
                Warehouses = JsonSerializer.Deserialize<List<Warehouse>>(json) ?? new();
            }
            if (File.Exists(productsPath))
            {
                var json = File.ReadAllText(productsPath);
                Products = JsonSerializer.Deserialize<List<Product>>(json) ?? new();
            }
            if (File.Exists(movementsPath))
            {
                var json = File.ReadAllText(movementsPath);
                StockMovements = JsonSerializer.Deserialize<List<StockMovement>>(json) ?? new();
            }
        }
    }
}
