namespace PrestamistaRD.Models
{
    public class Pago
    {
        public int Id { get; set; }
        public int PrestamoId { get; set; }
        public DateTime FechaPago { get; set; }

        // Tipo de pago (Interes, Total, InteresAbono, etc.)
        public string TipoPago { get; set; } = "Interes";

        // Periodo en formato YYYY-MM
        public string Periodo { get; set; } = "";

        public decimal MontoInteres { get; set; }
        public decimal MontoMora { get; set; }
        public decimal MontoCapital { get; set; }
        public decimal TotalPagado { get; set; }

        public string? Observacion { get; set; }

        // Frecuencia de pago (Mensual o Quincenal)
        public string Frecuencia { get; set; } = "Mensual";

        public int? CuotaId { get; set; }

    }
}
