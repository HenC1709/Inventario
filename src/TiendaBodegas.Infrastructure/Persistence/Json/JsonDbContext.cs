using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TiendaBodegas.Domain.Entities;

namespace TiendaBodegas.Infrastructure.Persistence.Json
{
    public class JsonDbContext
    {
        public List<Bodega> Bodegas { get; private set; } = new List<Bodega>();
        public List<Producto> Productos { get; private set; } = new List<Producto>();
        public List<Ticket> Tickets { get; private set; } = new List<Ticket>();

        public void GuardarJson()
        {
            var dirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            Directory.CreateDirectory(dirPath);

            var opciones = new JsonSerializerOptions { WriteIndented = true };

            File.WriteAllText(Path.Combine(dirPath, "bodegas.json"), JsonSerializer.Serialize(Bodegas, opciones));
            File.WriteAllText(Path.Combine(dirPath, "productos.json"), JsonSerializer.Serialize(Productos, opciones));
            File.WriteAllText(Path.Combine(dirPath, "tickets.json"), JsonSerializer.Serialize(Tickets, opciones));
        }

        public void ObtenerJson()
        {
            var dirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
           Directory.CreateDirectory(dirPath);

           var bodegasPath = Path.Combine(dirPath, "bodegas.json");
           var productosPath = Path.Combine(dirPath, "productos.json");
           var ticketsPath = Path.Combine(dirPath, "tickets.json");
            if (File.Exists(bodegasPath))
            {
             var json = File.ReadAllText(bodegasPath);
             Bodegas = JsonSerializer.Deserialize<List<Bodega>>(json) ?? new List<Bodega>();
            }
            if (File.Exists(productosPath))
            {
             var json = File.ReadAllText(productosPath);
             Productos = JsonSerializer.Deserialize<List<Producto>>(json) ?? new List<Producto>();
            }
            if (File.Exists(ticketsPath))
            {
             var json = File.ReadAllText(ticketsPath);
            Tickets = JsonSerializer.Deserialize<List<Ticket>>(json) ?? new List<Ticket>();
            }
        }
    }
}
