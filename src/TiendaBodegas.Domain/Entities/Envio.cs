using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.Domain.Entities;

public class Envio
{
    public int Id { get; private set; }
    public int TicketId { get; private set; } // El movimiento de stock que originó el envío
    public DateTime FechaCreacion { get; private set; }
    public DateTime? FechaEntregaEstimada { get; private set; }
    public EstadoEnvio Estado { get; private set; }
    public string DireccionDestino { get; private set; } = string.Empty;
    public string Transportadora { get; private set; } = string.Empty;
    public string GuiaSeguimiento { get; private set; } = string.Empty;

    // Propiedad de navegación
    public Ticket Ticket { get; private set; } = null!;

    public Envio() { }

    public Envio(int ticketId, string direccion, string transportadora)
    {
        TicketId = ticketId;
        DireccionDestino = direccion;
        Transportadora = transportadora;
        FechaCreacion = DateTime.Now;
        Estado = EstadoEnvio.Pendiente; // Todo envío nace pendiente
    }

    // Método para cambiar el estado de forma controlada
    public void ActualizarEstado(EstadoEnvio nuevoEstado)
    {
        // Aquí podrías meter lógica: "Si ya está Entregado, no puede volver a Pendiente"
        Estado = nuevoEstado;
    }
}