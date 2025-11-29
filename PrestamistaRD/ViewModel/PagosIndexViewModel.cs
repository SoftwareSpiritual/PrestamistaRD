namespace PrestamistaRD.Models.ViewModels
{
    public class PagosIndexViewModel
    {
        // Datos del préstamo
        public int PrestamoId { get; set; }
        public string ClienteNombre { get; set; }
        public decimal MontoOriginal { get; set; }
        public bool EsPlazoDefinido { get; set; }

        // Lista de pagos normales
        public List<Pago>? Pagos { get; set; }

        // Cuotas originales (EsReenganche = 0)
        public List<Cuota>? CuotasOriginales { get; set; }

        // Cuotas nuevas por reenganche (EsReenganche = 1)
        public List<Cuota>? CuotasReenganche { get; set; }
    }
}
