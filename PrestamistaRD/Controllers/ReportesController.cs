using Microsoft.AspNetCore.Mvc;
using PrestamistaRD.Data;
using PrestamistaRD.Models;
using MySql.Data.MySqlClient;

namespace PrestamistaRD.Controllers
{
    /// <summary>
    /// Controlador encargado de la generación de reportes.
    /// Permite filtrar información por cliente, préstamo y rango de fechas,
    /// mostrando indicadores financieros y estadísticos.
    /// </summary>
    public class ReportesController : Controller
    {
        private readonly Db _db;
        public ReportesController(Db db) => _db = db;

        /// <summary>
        /// Muestra el dashboard de reportes con filtros opcionales:
        /// cliente, préstamo y rango de fechas.
        /// Calcula totales de clientes, préstamos, pagos, capital pendiente,
        /// intereses cobrados y capital prestado.
        /// </summary>
        public IActionResult Index(int? clienteId, int? prestamoId, DateTime? fechaInicio, DateTime? fechaFin)
        {
            var model = new DashboardViewModel();

            using var con = _db.GetConn();
            con.Open();

            // 🔹 Filtros dinámicos por cliente y préstamo
            var wherePrestamo = "";
            if (clienteId.HasValue) wherePrestamo += $" AND p.ClienteId = {clienteId.Value}";
            if (prestamoId.HasValue) wherePrestamo += $" AND p.Id = {prestamoId.Value}";

            // 🔹 Filtros dinámicos por fechas
            var whereFechas = "";
            if (fechaInicio.HasValue && fechaFin.HasValue)
                whereFechas = $" AND DATE(pg.FechaPago) BETWEEN '{fechaInicio.Value:yyyy-MM-dd}' AND '{fechaFin.Value:yyyy-MM-dd}'";
            else if (fechaInicio.HasValue)
                whereFechas = $" AND DATE(pg.FechaPago) >= '{fechaInicio.Value:yyyy-MM-dd}'";
            else if (fechaFin.HasValue)
                whereFechas = $" AND DATE(pg.FechaPago) <= '{fechaFin.Value:yyyy-MM-dd}'";

            // 📌 Total clientes
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM Clientes", con))
                model.TotalClientes = Convert.ToInt32(cmd.ExecuteScalar());

            // 📌 Total préstamos activos
            using (var cmd = new MySqlCommand($"SELECT COUNT(*) FROM Prestamos p WHERE p.Estado='Activo' {wherePrestamo}", con))
                model.TotalPrestamosActivos = Convert.ToInt32(cmd.ExecuteScalar());

            // 📌 Capital prestado en préstamos activos
            using (var cmd = new MySqlCommand($"SELECT IFNULL(SUM(p.MontoCapital),0) FROM Prestamos p WHERE p.Estado='Activo' {wherePrestamo}", con))
                model.TotalCapitalPrestado = Convert.ToDecimal(cmd.ExecuteScalar());

            // 📌 Total pagos registrados
            using (var cmd = new MySqlCommand($@"
                SELECT IFNULL(SUM(pg.TotalPagado),0)
                FROM Pagos pg
                INNER JOIN Prestamos p ON pg.PrestamoId = p.Id
                WHERE 1=1 {wherePrestamo} {whereFechas}", con))
                model.TotalPagos = Convert.ToDecimal(cmd.ExecuteScalar());

            // 📌 Pendiente por cobrar
            using (var cmd = new MySqlCommand($"SELECT IFNULL(SUM(p.MontoCapital),0) FROM Prestamos p WHERE p.Estado='Activo' {wherePrestamo}", con))
            {
                var capital = Convert.ToDecimal(cmd.ExecuteScalar());

                using var cmdPagosCapital = new MySqlCommand($@"
                    SELECT IFNULL(SUM(pg.MontoCapital),0)
                    FROM Pagos pg
                    INNER JOIN Prestamos p ON pg.PrestamoId = p.Id
                    WHERE 1=1 {wherePrestamo} {whereFechas}", con);

                var capitalPagado = Convert.ToDecimal(cmdPagosCapital.ExecuteScalar());

                model.TotalPendiente = capital - capitalPagado;
                if (model.TotalPendiente < 0) model.TotalPendiente = 0;
            }

            // 📌 Intereses cobrados
            using (var cmd = new MySqlCommand($@"
                SELECT IFNULL(SUM(pg.MontoInteres),0)
                FROM Pagos pg
                INNER JOIN Prestamos p ON pg.PrestamoId = p.Id
                WHERE 1=1 {wherePrestamo} {whereFechas}", con))
                model.TotalIntereses = Convert.ToDecimal(cmd.ExecuteScalar());

            // 🔹 Cargar combos de clientes y préstamos para los filtros
            ViewBag.Clientes = ObtenerClientes();
            ViewBag.Prestamos = ObtenerPrestamos();

            return View(model);
        }

        /// <summary>
        /// Obtiene la lista de todos los clientes.
        /// </summary>
        private List<Cliente> ObtenerClientes()
        {
            var lista = new List<Cliente>();
            using var con = _db.GetConn();
            con.Open();
            string sql = "SELECT Id, NombreCompleto FROM Clientes";
            using var cmd = new MySqlCommand(sql, con);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new Cliente
                {
                    Id = r.GetInt32("Id"),
                    NombreCompleto = r.GetString("NombreCompleto")
                });
            }
            return lista;
        }

        /// <summary>
        /// Obtiene la lista de préstamos junto con el nombre del cliente.
        /// </summary>
        private List<Prestamo> ObtenerPrestamos()
        {
            var lista = new List<Prestamo>();
            using var con = _db.GetConn();
            con.Open();
            string sql = @"SELECT p.Id, c.NombreCompleto, p.MontoCapital 
                           FROM Prestamos p
                           INNER JOIN Clientes c ON p.ClienteId = c.Id";
            using var cmd = new MySqlCommand(sql, con);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new Prestamo
                {
                    Id = r.GetInt32("Id"),
                    ClienteNombre = r.GetString("NombreCompleto"),
                    MontoCapital = r.GetDecimal("MontoCapital")
                });
            }
            return lista;
        }
    }
}
