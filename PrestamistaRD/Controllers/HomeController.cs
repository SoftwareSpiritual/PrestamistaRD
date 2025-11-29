using Microsoft.AspNetCore.Mvc;
using PrestamistaRD.Data;
using PrestamistaRD.Models;
using MySql.Data.MySqlClient;

namespace PrestamistaRD.Controllers
{
    /// <summary>
    /// Controlador principal que gestiona el Dashboard del sistema.
    /// Muestra indicadores globales de clientes, préstamos, pagos,
    /// capital, intereses y mora.
    /// </summary>
    public class HomeController : Controller
    {
        private readonly Db _db;
        public HomeController(Db db) => _db = db;

        /// <summary>
        /// Acción principal del dashboard.
        /// Calcula estadísticas financieras y de clientes para
        /// mostrarlas en la vista principal.
        /// </summary>
        public IActionResult Index()
        {
            var model = new DashboardViewModel();

            using var con = _db.GetConn();
            con.Open();

            // 🔹 Actualiza automáticamente préstamos vencidos
            using (var cmd = new MySqlCommand(@"
                UPDATE Prestamos
                SET Estado = 'Atrasado'
                WHERE Estado = 'Activo' AND DiaPago < CURDATE();", con))
            {
                cmd.ExecuteNonQuery();
            }

            // 📌 Total de clientes registrados
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM Clientes", con))
                model.TotalClientes = Convert.ToInt32(cmd.ExecuteScalar());

            // 📌 Total de préstamos activos
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM Prestamos WHERE Estado='Activo'", con))
                model.TotalPrestamosActivos = Convert.ToInt32(cmd.ExecuteScalar());

            // 📌 Total de préstamos pagados
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM Prestamos WHERE Estado='Pagado'", con))
                model.TotalPrestamosPagados = Convert.ToInt32(cmd.ExecuteScalar());

            // 📌 Total de préstamos atrasados
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM Prestamos WHERE Estado='Atrasado'", con))
                model.TotalPrestamosAtrasados = Convert.ToInt32(cmd.ExecuteScalar());

            // 📌 Capital total prestado (capital inicial de todos los préstamos)
            using (var cmd = new MySqlCommand("SELECT IFNULL(SUM(MontoOriginal),0) FROM Prestamos", con))
                model.TotalCapitalPrestado = Convert.ToDecimal(cmd.ExecuteScalar());

            // 📌 Total de pagos recibidos (capital + interés + mora)
            using (var cmd = new MySqlCommand("SELECT IFNULL(SUM(TotalPagado),0) FROM Pagos", con))
                model.TotalPagos = Convert.ToDecimal(cmd.ExecuteScalar());

            // 📌 Total pendiente (capital en préstamos activos)
            using (var cmd = new MySqlCommand("SELECT IFNULL(SUM(MontoCapital),0) FROM Prestamos WHERE Estado='Activo'", con))
                model.TotalPendiente = Convert.ToDecimal(cmd.ExecuteScalar());

            // 📌 Intereses efectivamente cobrados
            using (var cmd = new MySqlCommand("SELECT IFNULL(SUM(MontoInteres),0) FROM Pagos", con))
                model.TotalIntereses = Convert.ToDecimal(cmd.ExecuteScalar());

            // 📌 Monto total cobrado por mora
            using (var cmd = new MySqlCommand("SELECT IFNULL(SUM(MontoMora),0) FROM Pagos", con))
                model.TotalMora = Convert.ToDecimal(cmd.ExecuteScalar());

            // 📌 Abonos a capital (pagos mixtos: interés + abono)
            using (var cmd = new MySqlCommand("SELECT IFNULL(SUM(MontoCapital),0) FROM Pagos WHERE TipoPago='InteresAbono'", con))
                model.TotalAbonos = Convert.ToDecimal(cmd.ExecuteScalar());

            return View(model);
        }
    }
}
