namespace PrestamistaRD.Models
{
    public class DashboardViewModel
    {
        // Totales generales
        public int TotalClientes { get; set; }

        // Préstamos
        public int TotalPrestamosActivos { get; set; }
        public int TotalPrestamosPagados { get; set; }   // ✅ Nuevos
        public int TotalPrestamosAtrasados { get; set; } // ✅ Nuevos

        // Dinero
        public decimal TotalCapitalPrestado { get; set; }
        public decimal TotalPagos { get; set; }
        public decimal TotalPendiente { get; set; }
        public decimal TotalIntereses { get; set; }

        // Mora y abonos
        public decimal TotalMora { get; set; }   // ✅ Nuevo
        public decimal TotalAbonos { get; set; } // ✅ Nuevo
    }
}
