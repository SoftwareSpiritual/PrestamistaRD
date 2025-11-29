using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PrestamistaRD.Models
{
    public class Prestamo
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un cliente.")]
        public int ClienteId { get; set; }

        // 🔹 Capital actual (se va reduciendo con los pagos y abonos)
        [Required(ErrorMessage = "El monto capital es obligatorio.")]
        [Range(1, double.MaxValue, ErrorMessage = "El monto capital debe ser mayor a 0.")]
        public decimal MontoCapital { get; set; }

        // 🔹 Capital original (valor inicial cuando se creó el préstamo)
        public decimal MontoOriginal { get; set; }

        // Porcentaje de interés mensual definido por el usuario
        [Required(ErrorMessage = "El porcentaje de interés es obligatorio.")]
        [Range(0.01, 100, ErrorMessage = "El porcentaje debe estar entre 0.01 y 100.")]
        public decimal PorcentajeMensual { get; set; }

        // Fecha en que se desembolsó el préstamo
        [Required(ErrorMessage = "La fecha de desembolso es obligatoria.")]
        [DataType(DataType.Date)]
        public DateTime FechaDesembolso { get; set; }

        // Fecha objetivo del primer pago
        [Required(ErrorMessage = "La fecha de pago es obligatoria.")]
        [DataType(DataType.Date)]
        public DateTime DiaPago { get; set; }

        // Mora fija por atraso
        [Range(0, double.MaxValue, ErrorMessage = "La mora fija debe ser mayor o igual a 0.")]
        public decimal MoraFija { get; set; } = 150m;

        // Estado del préstamo (Activo, Pagado, Atrasado)
        [Required(ErrorMessage = "Debe seleccionar un estado para el préstamo.")]
        public string Estado { get; set; } = "Activo";

        // Indica si el préstamo es a plazo definido (true/false)
        public bool EsPlazoDefinido { get; set; } = false;

        // Número de meses si el préstamo es a plazo definido
        [Range(1, 120, ErrorMessage = "El número de meses debe estar entre 1 y 120.")]
        public int? NumeroMeses { get; set; }

        [MaxLength(500, ErrorMessage = "La observación no puede superar los 500 caracteres.")]
        public string? Observacion { get; set; }

        // Propiedad de apoyo (no mapeada a BD)
        public string? ClienteNombre { get; set; }

        // 🔹 Relación con pagos
        public List<Pago>? Pagos { get; set; }

        // 🔹 Relación con ampliaciones (Reenganches)
        public List<Ampliacion>? Ampliaciones { get; set; }

        // 🔹 Listado general de cuotas (si quieres usarlo para debug)
        public List<Cuota>? Cuotas { get; set; }

        // ✅ Nuevas propiedades para separar las cuotas
        public List<Cuota> CuotasOriginales { get; set; } = new();
        public List<Cuota> CuotasReenganche { get; set; } = new();
    }
}
