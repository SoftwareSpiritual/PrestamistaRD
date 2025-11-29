namespace PrestamistaRD.Models
{
    public class Cliente
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = "";
        public string Cedula { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string Direccion { get; set; } = "";
        public bool Estado { get; set; } = true;
    }
}
