namespace PrestamistaRD.Models.ViewModels
{
    public class AmpliacionViewModel
    {
        public int PrestamoId { get; set; }
        public string ClienteNombre { get; set; } = "";
        public decimal SaldoActual { get; set; }
        public decimal MontoOriginal { get; set; }
        public decimal MontoAmpliado { get; set; }
        public string? Observacion { get; set; }
    }
}
