namespace PrestamistaRD.Models
{
    public class Ampliacion
    {
        public int Id { get; set; }
        public int PrestamoId { get; set; }            // Relación con préstamo
        public DateTime Fecha { get; set; } = DateTime.Now;
        public decimal MontoAmpliado { get; set; }     // 🔹 Igual que en la BD
        public string? Observacion { get; set; }

        // 🔹 Para mostrar info adicional en las vistas
        public string? ClienteNombre { get; set; }
    }
}
