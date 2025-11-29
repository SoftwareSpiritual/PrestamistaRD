namespace PrestamistaRD.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Correo { get; set; } = "";
        public string ClaveHash { get; set; } = "";
        public string Rol { get; set; } = "Operador"; // Admin u Operador
        public bool Estado { get; set; } = true;
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
