using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using PrestamistaRD.Data;
using PrestamistaRD.Models;
using PrestamistaRD.Models.ViewModels;

namespace PrestamistaRD.Controllers
{
    /// <summary>
    /// Controlador encargado de la gestión de pagos.
    /// Permite registrar, consultar y detallar pagos,
    /// incluyendo cuotas definidas y pagos normales.
    /// </summary>
    public class PagosController : Controller
    {
        private readonly Db _db;
        public PagosController(Db db) => _db = db;

        /// <summary>
        /// Muestra la lista de pagos organizados por préstamo.
        /// Diferencia entre préstamos con cuotas definidas y pagos normales.
        /// </summary>
        public IActionResult Index()
        {
            var lista = new List<PagosIndexViewModel>();

            using var con = _db.GetConn();
            con.Open();

            // 🔹 Buscar préstamos activos o atrasados
            string sqlPrestamos = @"SELECT p.Id, p.MontoCapital, p.MontoOriginal, p.EsPlazoDefinido, p.NumeroMeses,   -- ✅ agregado MontoOriginal
                                           c.NombreCompleto
                                    FROM Prestamos p
                                    INNER JOIN Clientes c ON p.ClienteId = c.Id
                                    WHERE p.Estado NOT IN ('Cerrado','Pagado')";
            using var cmdPrestamos = new MySqlCommand(sqlPrestamos, con);
            using var rPrestamos = cmdPrestamos.ExecuteReader();

            var prestamos = new List<(int Id, string Cliente, decimal Capital, decimal Original, bool EsPlazo)>(); // ✅ agregado Original
            while (rPrestamos.Read())
            {
                prestamos.Add((
                    rPrestamos.GetInt32("Id"),
                    rPrestamos.GetString("NombreCompleto"),
                    rPrestamos.GetDecimal("MontoCapital"),
                    rPrestamos.GetDecimal("MontoOriginal"), // ✅ agregado
                    rPrestamos.GetBoolean("EsPlazoDefinido")
                ));
            }
            rPrestamos.Close();

            // 🔹 Procesar cada préstamo según sea por cuotas o normal
            foreach (var p in prestamos)
            {
                var vm = new PagosIndexViewModel
                {
                    PrestamoId = p.Id,
                    ClienteNombre = p.Cliente,
                    MontoOriginal = p.Original, // ✅ ahora se pasa el original
                    EsPlazoDefinido = p.EsPlazo
                };

                if (p.EsPlazo)
                {
                    // 📌 Obtener cuotas y separar
                    vm.CuotasOriginales = new List<Cuota>();
                    vm.CuotasReenganche = new List<Cuota>();

                    string sqlCuotas = @"
    SELECT c.*,
           (
               SELECT p.Id
               FROM Pagos p
               WHERE p.CuotaId = c.Id
               ORDER BY p.FechaPago DESC
               LIMIT 1
           ) AS PagoId
    FROM Cuotas c
    WHERE c.PrestamoId=@id
    ORDER BY c.NumeroCuota";
                    using var cmd = new MySqlCommand(sqlCuotas, con);
                    cmd.Parameters.AddWithValue("@id", p.Id);
                    using var r = cmd.ExecuteReader();

                    while (r.Read())
                    {
                        var cuota = new Cuota
                        {
                            Id = r.GetInt32("Id"),
                            PrestamoId = r.GetInt32("PrestamoId"),
                            NumeroCuota = r.GetInt32("NumeroCuota"),
                            FechaVencimiento = r.GetDateTime("FechaVencimiento"),
                            MontoCapital = r.GetDecimal("MontoCapital"),
                            MontoInteres = r.GetDecimal("MontoInteres"),
                            MontoTotal = r.GetDecimal("MontoTotal"),
                            Estado = r.GetString("Estado"),
                            EsReenganche = r.GetBoolean("EsReenganche"),
                            PagoId = r.IsDBNull(r.GetOrdinal("PagoId")) ? null : r.GetInt32("PagoId")
                        };

                        if (cuota.EsReenganche)
                            vm.CuotasReenganche.Add(cuota);
                        else
                            vm.CuotasOriginales.Add(cuota);
                    }
                    r.Close();
                }
                else
                {
                    // 📌 Obtener pagos realizados
                    string sqlPagos = @"SELECT * FROM Pagos WHERE PrestamoId=@id ORDER BY FechaPago DESC";
                    using var cmd = new MySqlCommand(sqlPagos, con);
                    cmd.Parameters.AddWithValue("@id", p.Id);
                    using var r = cmd.ExecuteReader();

                    vm.Pagos = new List<Pago>();
                    while (r.Read())
                    {
                        vm.Pagos.Add(new Pago
                        {
                            Id = r.GetInt32("Id"),
                            PrestamoId = r.GetInt32("PrestamoId"),
                            FechaPago = r.GetDateTime("FechaPago"),
                            TipoPago = r.GetString("TipoPago"),
                            Periodo = r.GetString("Periodo"),
                            Frecuencia = r.IsDBNull(r.GetOrdinal("Frecuencia")) ? "Mensual" : r.GetString("Frecuencia"),
                            MontoInteres = r.GetDecimal("MontoInteres"),
                            MontoMora = r.GetDecimal("MontoMora"),
                            MontoCapital = r.GetDecimal("MontoCapital"),
                            TotalPagado = r.GetDecimal("TotalPagado"),
                            Observacion = r.IsDBNull(r.GetOrdinal("Observacion")) ? null : r.GetString("Observacion")
                        });
                    }
                    r.Close();
                }

                lista.Add(vm);
            }

            return View(lista);
        }

        /// <summary>
        /// Muestra el formulario para crear un nuevo pago.
        /// </summary>
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Prestamos = ObtenerPrestamos();
            return View();
        }

        /// <summary>
        /// Carga la vista de pago de cuota específica (pago directo a cuota).
        /// </summary>
        [HttpGet]
        public IActionResult PagarCuota(int cuotaId)
        {
            using var con = _db.GetConn();
            con.Open();

            string sql = @"SELECT c.*, p.PorcentajeMensual, cli.NombreCompleto
                           FROM Cuotas c
                           INNER JOIN Prestamos p ON c.PrestamoId = p.Id
                           INNER JOIN Clientes cli ON p.ClienteId = cli.Id
                           WHERE c.Id=@id";
            using var cmd = new MySqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", cuotaId);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return NotFound();

            var pago = new Pago
            {
                PrestamoId = r.GetInt32("PrestamoId"),
                FechaPago = DateTime.Today,
                TipoPago = "InteresAbono",
                Periodo = $"{DateTime.Today:yyyy-MM}",
                Frecuencia = "Mensual",
                MontoCapital = r.GetDecimal("MontoCapital"),
                MontoInteres = r.GetDecimal("MontoInteres"),
                TotalPagado = r.GetDecimal("MontoTotal"),
                Observacion = $"Pago de cuota #{r.GetInt32("NumeroCuota")}",
                CuotaId = r.GetInt32("Id")
            };

            ViewBag.CuotaId = r.GetInt32("Id");
            r.Close();
            ViewBag.Prestamos = ObtenerPrestamos();

            return View("Create", pago);
        }

        /// <summary>
        /// Procesa el registro de un nuevo pago.
        /// Soporta pagos normales y pagos de cuotas.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Pago model, int? cuotaId)
        {
            // 🔹 Validación para evitar doble clic
            if (model.TotalPagado <= 0)
            {
                model.TotalPagado = model.MontoInteres + model.MontoMora + model.MontoCapital;
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Prestamos = ObtenerPrestamos();
                return View(model);
            }

            using var con = _db.GetConn();
            con.Open();

            // 🔹 Ajuste para intereses de préstamos normales (Caso B)
            if (!cuotaId.HasValue)
            {
                string sqlPrestamo = @"SELECT MontoOriginal, PorcentajeMensual, EsPlazoDefinido, NumeroMeses 
                                       FROM Prestamos WHERE Id=@id";
                using var cmdPrestamo = new MySqlCommand(sqlPrestamo, con);
                cmdPrestamo.Parameters.AddWithValue("@id", model.PrestamoId);
                using var rPrestamo = cmdPrestamo.ExecuteReader();
                if (rPrestamo.Read())
                {
                    decimal montoOriginal = rPrestamo.GetDecimal("MontoOriginal");
                    decimal porcentaje = rPrestamo.GetDecimal("PorcentajeMensual");
                    bool esPlazo = rPrestamo.GetBoolean("EsPlazoDefinido");
                    int? meses = rPrestamo.IsDBNull(rPrestamo.GetOrdinal("NumeroMeses")) ? null : rPrestamo.GetInt32("NumeroMeses");

                    if (!esPlazo)
                    {
                        // ✅ Interés siempre fijo sobre monto original
                        model.MontoInteres = (montoOriginal * porcentaje) / 100m;
                    }
                }
                rPrestamo.Close();
            }
           
            // Guardar pago
            string sql = @"INSERT INTO Pagos 
               (PrestamoId, CuotaId, FechaPago, TipoPago, Periodo, Frecuencia,
                MontoInteres, MontoMora, MontoCapital, TotalPagado, Observacion)
               VALUES (@p,@cId,@f,@t,@pe,@fr,@i,@mo,@c,@tp,@o)";
            using var cmd = new MySqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@p", model.PrestamoId);
            cmd.Parameters.AddWithValue("@cId", cuotaId.HasValue ? cuotaId.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@f", model.FechaPago);
            cmd.Parameters.AddWithValue("@t", model.TipoPago);
            cmd.Parameters.AddWithValue("@pe", model.Periodo ?? "");
            cmd.Parameters.AddWithValue("@fr", model.Frecuencia);
            cmd.Parameters.AddWithValue("@i", model.MontoInteres);
            cmd.Parameters.AddWithValue("@mo", model.MontoMora);
            cmd.Parameters.AddWithValue("@c", model.MontoCapital);
            cmd.Parameters.AddWithValue("@tp", model.TotalPagado);
            cmd.Parameters.AddWithValue("@o", model.Observacion ?? (object)DBNull.Value);

            cmd.ExecuteNonQuery();


            // 🔹 Si es pago de cuota, marcar como pagada y actualizar préstamo
            if (cuotaId.HasValue)
            {
                string sqlUpdateCuota = "UPDATE Cuotas SET Estado='Pagada' WHERE Id=@id";
                using var cmdCuota = new MySqlCommand(sqlUpdateCuota, con);
                cmdCuota.Parameters.AddWithValue("@id", cuotaId.Value);
                cmdCuota.ExecuteNonQuery();

                string sqlUpdatePrestamo = @"UPDATE Prestamos 
                                             SET MontoCapital = MontoCapital - @capital 
                                             WHERE Id=@id";
                using var cmdPrestamo = new MySqlCommand(sqlUpdatePrestamo, con);
                cmdPrestamo.Parameters.AddWithValue("@capital", model.MontoCapital);
                cmdPrestamo.Parameters.AddWithValue("@id", model.PrestamoId);
                cmdPrestamo.ExecuteNonQuery();
            }
            else
            {
                // 🔹 Caso: pago normal
                if (model.TipoPago == "InteresAbono" && model.MontoCapital > 0)
                {
                    string sqlUpdate = "UPDATE Prestamos SET MontoCapital = MontoCapital - @abono WHERE Id=@id";
                    using var cmdUpdate = new MySqlCommand(sqlUpdate, con);
                    cmdUpdate.Parameters.AddWithValue("@abono", model.MontoCapital);
                    cmdUpdate.Parameters.AddWithValue("@id", model.PrestamoId);
                    cmdUpdate.ExecuteNonQuery();
                }
                else if (model.TipoPago == "Total")
                {
                    string sqlUpdate = "UPDATE Prestamos SET Estado='Pagado', MontoCapital=0 WHERE Id=@id";
                    using var cmdUpdate = new MySqlCommand(sqlUpdate, con);
                    cmdUpdate.Parameters.AddWithValue("@id", model.PrestamoId);
                    cmdUpdate.ExecuteNonQuery();
                }
            }

            // ✅ Verificar si ya se terminó de pagar con tolerancia
            string sqlCheck = "SELECT MontoCapital FROM Prestamos WHERE Id=@id";
            using var cmdCheck = new MySqlCommand(sqlCheck, con);
            cmdCheck.Parameters.AddWithValue("@id", model.PrestamoId);
            var saldo = Convert.ToDecimal(cmdCheck.ExecuteScalar());

            if (saldo <= 0.05m)
            {
                string sqlEstado = "UPDATE Prestamos SET Estado='Pagado', MontoCapital=0 WHERE Id=@id";
                using var cmdEstado = new MySqlCommand(sqlEstado, con);
                cmdEstado.Parameters.AddWithValue("@id", model.PrestamoId);
                cmdEstado.ExecuteNonQuery();
            }

            TempData["Mensaje"] = "✅ Pago registrado correctamente";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Muestra los detalles de un pago específico.
        /// Incluye datos del cliente, préstamo y condiciones.
        /// </summary>
        public IActionResult Details(int id)
        {
            using var con = _db.GetConn();
            con.Open();
            string sql = @"SELECT p.*, c.NombreCompleto, pr.MontoOriginal, pr.PorcentajeMensual   -- 🔹 ajuste CapitalOriginal -> MontoOriginal
                           FROM Pagos p
                           INNER JOIN Prestamos pr ON p.PrestamoId = pr.Id
                           INNER JOIN Clientes c ON pr.ClienteId = c.Id
                           WHERE p.Id=@id";
            using var cmd = new MySqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", id);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return NotFound();

            var pago = new Pago
            {
                Id = r.GetInt32("Id"),
                PrestamoId = r.GetInt32("PrestamoId"),
                FechaPago = r.GetDateTime("FechaPago"),
                TipoPago = r.GetString("TipoPago"),
                Periodo = r.GetString("Periodo"),
                Frecuencia = r.GetString("Frecuencia"),
                MontoInteres = r.GetDecimal("MontoInteres"),
                MontoMora = r.GetDecimal("MontoMora"),
                MontoCapital = r.GetDecimal("MontoCapital"),
                TotalPagado = r.GetDecimal("TotalPagado"),
                Observacion = r.IsDBNull(r.GetOrdinal("Observacion")) ? null : r.GetString("Observacion")
            };

            ViewBag.Cliente = r.GetString("NombreCompleto");
            ViewBag.InteresMensual = r.GetDecimal("PorcentajeMensual");
            ViewBag.CapitalOriginal = r.GetDecimal("MontoOriginal"); // ✅ ajuste

            return View(pago);
        }

        /// <summary>
        /// Obtiene la lista de préstamos activos o atrasados,
        /// incluyendo nombre de cliente, capital y condiciones.
        /// </summary>
        private List<Prestamo> ObtenerPrestamos()
        {
            var lista = new List<Prestamo>();
            using var con = _db.GetConn();
            con.Open();
            string sql = @"SELECT p.Id, c.NombreCompleto, p.MontoCapital, p.MontoOriginal,  -- ✅ agregado MontoOriginal
                                  p.PorcentajeMensual, p.DiaPago, p.EsPlazoDefinido, p.NumeroMeses
                           FROM Prestamos p
                           INNER JOIN Clientes c ON p.ClienteId = c.Id
                           WHERE p.Estado='Activo' OR p.Estado='Atrasado'";
            using var cmd = new MySqlCommand(sql, con);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new Prestamo
                {
                    Id = r.GetInt32("Id"),
                    ClienteNombre = r.GetString("NombreCompleto"),
                    MontoCapital = r.GetDecimal("MontoCapital"),
                    MontoOriginal = r.GetDecimal("MontoOriginal"), // ✅ agregado
                    PorcentajeMensual = r.GetDecimal("PorcentajeMensual"),
                    DiaPago = r.GetDateTime("DiaPago"),
                    EsPlazoDefinido = r.GetBoolean("EsPlazoDefinido"),
                    NumeroMeses = r.IsDBNull(r.GetOrdinal("NumeroMeses")) ? null : r.GetInt32("NumeroMeses")
                });
            }
            return lista;
        }
    }
}
