namespace PrestamistaRD.Models
{
    public class Cuota
    {
        public int Id { get; set; }
        public int PrestamoId { get; set; }

        // Número de la cuota (1, 2, 3, etc.)
        public int NumeroCuota { get; set; }

        // Fecha en que vence esta cuota
        public DateTime FechaVencimiento { get; set; }

        // Capital correspondiente a esta cuota
        public decimal MontoCapital { get; set; }

        // Interés correspondiente a esta cuota
        public decimal MontoInteres { get; set; }

        // Capital + Interés
        public decimal MontoTotal { get; set; }

        // Estado de la cuota
        public string Estado { get; set; } = "Pendiente"; // Pendiente, Pagada, Vencida

        // 🔹 Nueva propiedad para identificar cuotas de reenganche
        public bool EsReenganche { get; set; } = false;

        // Propiedades de apoyo (no mapeadas)
        public string? ClienteNombre { get; set; }

        // 🔹 Nuevo campo para enlazar al último pago de esa cuota
        public int? PagoId { get; set; }
    }
}
