using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using PrestamistaRD.Data;
using PrestamistaRD.Models;
using PrestamistaRD.Models.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;


namespace PrestamistaRD.Controllers
{
    public class PrestamosController : Controller
    {
        private readonly Db _db;
        public PrestamosController(Db db) => _db = db;

        // =====================================================
        // INDEX → Solo activos y atrasados
        // =====================================================
        public IActionResult Index()
        {
            var lista = new List<Prestamo>();

            using var con = _db.GetConn();
            con.Open();
            string sql = @"SELECT p.*, c.NombreCompleto 
                           FROM Prestamos p
                           INNER JOIN Clientes c ON p.ClienteId = c.Id
                           WHERE p.Estado IN ('Activo','Atrasado')
                           ORDER BY p.FechaDesembolso DESC";
            using var cmd = new MySqlCommand(sql, con);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new Prestamo
                {
                    Id = r.GetInt32("Id"),
                    ClienteId = r.GetInt32("ClienteId"),
                    MontoCapital = r.GetDecimal("MontoCapital"),
                    MontoOriginal = r.GetDecimal("MontoOriginal"),
                    PorcentajeMensual = r.GetDecimal("PorcentajeMensual"),
                    FechaDesembolso = r.GetDateTime("FechaDesembolso"),
                    DiaPago = r.GetDateTime("DiaPago"),
                    MoraFija = r.GetDecimal("MoraFija"),
                    Estado = r.GetString("Estado"),
                    EsPlazoDefinido = r.GetBoolean("EsPlazoDefinido"),
                    NumeroMeses = r.IsDBNull(r.GetOrdinal("NumeroMeses")) ? null : r.GetInt32("NumeroMeses"),
                    Observacion = r.IsDBNull(r.GetOrdinal("Observacion")) ? null : r.GetString("Observacion"),
                    ClienteNombre = r.GetString("NombreCompleto")
                });
            }
            r.Close();

            foreach (var prestamo in lista)
            {
                // Ampliaciones
                prestamo.Ampliaciones = new List<Ampliacion>();
                string sqlAmp = @"SELECT * FROM Ampliaciones WHERE PrestamoId=@id ORDER BY Fecha ASC";
                using var cmdAmp = new MySqlCommand(sqlAmp, con);
                cmdAmp.Parameters.AddWithValue("@id", prestamo.Id);
                using var rAmp = cmdAmp.ExecuteReader();
                while (rAmp.Read())
                {
                    prestamo.Ampliaciones.Add(new Ampliacion
                    {
                        Id = rAmp.GetInt32("Id"),
                        PrestamoId = rAmp.GetInt32("PrestamoId"),
                        Fecha = rAmp.GetDateTime("Fecha"),
                        MontoAmpliado = rAmp.GetDecimal("MontoAmpliado"),
                        Observacion = rAmp.IsDBNull(rAmp.GetOrdinal("Observacion")) ? null : rAmp.GetString("Observacion")
                    });
                }
                rAmp.Close();

                // 🔹 Cuotas (originales y reenganche en una sola consulta con orden por estado)
                prestamo.CuotasOriginales = new List<Cuota>();
                prestamo.CuotasReenganche = new List<Cuota>();

                string sqlCuotas = @"
    SELECT c.*,
           (SELECT p.Id 
            FROM Pagos p 
            WHERE p.CuotaId = c.Id
            ORDER BY p.FechaPago DESC 
            LIMIT 1) AS PagoId
    FROM Cuotas c
    WHERE c.PrestamoId=@id
    ORDER BY c.EsReenganche ASC,
             CASE c.Estado 
                 WHEN 'Pagada' THEN 1
                 WHEN 'Cancelada' THEN 2
                 WHEN 'Pendiente' THEN 3
                 WHEN 'Vencida' THEN 4
             END,
             c.FechaVencimiento ASC";

                using var cmdCuotas = new MySqlCommand(sqlCuotas, con);
                cmdCuotas.Parameters.AddWithValue("@id", prestamo.Id);
                using var rCuotas = cmdCuotas.ExecuteReader();
                while (rCuotas.Read())
                {
                    var cuota = new Cuota
                    {
                        Id = rCuotas.GetInt32("Id"),
                        PrestamoId = rCuotas.GetInt32("PrestamoId"),
                        NumeroCuota = rCuotas.GetInt32("NumeroCuota"),
                        FechaVencimiento = rCuotas.GetDateTime("FechaVencimiento"),
                        MontoCapital = rCuotas.GetDecimal("MontoCapital"),
                        MontoInteres = rCuotas.GetDecimal("MontoInteres"),
                        MontoTotal = rCuotas.GetDecimal("MontoTotal"),
                        Estado = rCuotas.GetString("Estado"),
                        EsReenganche = rCuotas.GetBoolean("EsReenganche"),
                        PagoId = rCuotas.IsDBNull(rCuotas.GetOrdinal("PagoId"))
                                 ? (int?)null
                                 : rCuotas.GetInt32("PagoId")
                    };

                    if (cuota.EsReenganche)
                        prestamo.CuotasReenganche.Add(cuota);
                    else
                        prestamo.CuotasOriginales.Add(cuota);
                }

                rCuotas.Close();

                // 🔹 Pagos
                prestamo.Pagos = new List<Pago>();
                string sqlPagos = @"SELECT * FROM Pagos WHERE PrestamoId=@id ORDER BY FechaPago ASC";
                using var cmdPagos = new MySqlCommand(sqlPagos, con);
                cmdPagos.Parameters.AddWithValue("@id", prestamo.Id);
                using var rPagos = cmdPagos.ExecuteReader();
                while (rPagos.Read())
                {
                    prestamo.Pagos.Add(new Pago
                    {
                        Id = rPagos.GetInt32("Id"),
                        PrestamoId = rPagos.GetInt32("PrestamoId"),
                        FechaPago = rPagos.GetDateTime("FechaPago"),
                        Periodo = rPagos.GetString("Periodo"),
                        MontoInteres = rPagos.GetDecimal("MontoInteres"),
                        MontoMora = rPagos.GetDecimal("MontoMora"),
                        MontoCapital = rPagos.GetDecimal("MontoCapital"),
                        TotalPagado = rPagos.GetDecimal("TotalPagado"),
                        TipoPago = rPagos.GetString("TipoPago"),
                        Frecuencia = rPagos.GetString("Frecuencia"),
                        Observacion = rPagos.IsDBNull(rPagos.GetOrdinal("Observacion")) ? null : rPagos.GetString("Observacion")
                    });
                }
                rPagos.Close();
            }

            return View(lista);
        }

        // =====================================================
        // LISTADO DE PRÉSTAMOS PAGADOS
        // =====================================================
        public IActionResult Pagados()
        {
            var lista = new List<Prestamo>();

            using var con = _db.GetConn();
            con.Open();
            string sql = @"SELECT p.*, c.NombreCompleto 
                   FROM Prestamos p
                   INNER JOIN Clientes c ON p.ClienteId = c.Id
                   WHERE p.Estado = 'Pagado'
                   ORDER BY p.FechaDesembolso DESC";
            using var cmd = new MySqlCommand(sql, con);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new Prestamo
                {
                    Id = r.GetInt32("Id"),
                    ClienteId = r.GetInt32("ClienteId"),
                    MontoCapital = r.GetDecimal("MontoCapital"),
                    MontoOriginal = r.GetDecimal("MontoOriginal"),
                    PorcentajeMensual = r.GetDecimal("PorcentajeMensual"),
                    FechaDesembolso = r.GetDateTime("FechaDesembolso"),
                    DiaPago = r.GetDateTime("DiaPago"),
                    MoraFija = r.GetDecimal("MoraFija"),
                    Estado = r.GetString("Estado"),
                    EsPlazoDefinido = r.GetBoolean("EsPlazoDefinido"),
                    NumeroMeses = r.IsDBNull(r.GetOrdinal("NumeroMeses")) ? null : r.GetInt32("NumeroMeses"),
                    Observacion = r.IsDBNull(r.GetOrdinal("Observacion")) ? null : r.GetString("Observacion"),
                    ClienteNombre = r.GetString("NombreCompleto")
                });
            }
            r.Close();

            // 🔹 Cargar ampliaciones y pagos de cada préstamo
            foreach (var prestamo in lista)
            {
                // Ampliaciones
                prestamo.Ampliaciones = new List<Ampliacion>();
                string sqlAmp = @"SELECT * FROM Ampliaciones WHERE PrestamoId=@id ORDER BY Fecha ASC";
                using (var cmdAmp = new MySqlCommand(sqlAmp, con))
                {
                    cmdAmp.Parameters.AddWithValue("@id", prestamo.Id);
                    using var rAmp = cmdAmp.ExecuteReader();
                    while (rAmp.Read())
                    {
                        prestamo.Ampliaciones.Add(new Ampliacion
                        {
                            Id = rAmp.GetInt32("Id"),
                            PrestamoId = rAmp.GetInt32("PrestamoId"),
                            Fecha = rAmp.GetDateTime("Fecha"),
                            MontoAmpliado = rAmp.GetDecimal("MontoAmpliado"),
                            Observacion = rAmp.IsDBNull(rAmp.GetOrdinal("Observacion")) ? null : rAmp.GetString("Observacion")
                        });
                    }
                }

                // Pagos
                prestamo.Pagos = new List<Pago>();
                string sqlPagos = @"SELECT * FROM Pagos WHERE PrestamoId=@id ORDER BY FechaPago ASC";
                using (var cmdPagos = new MySqlCommand(sqlPagos, con))
                {
                    cmdPagos.Parameters.AddWithValue("@id", prestamo.Id);
                    using var rPagos = cmdPagos.ExecuteReader();
                    while (rPagos.Read())
                    {
                        prestamo.Pagos.Add(new Pago
                        {
                            Id = rPagos.GetInt32("Id"),
                            PrestamoId = rPagos.GetInt32("PrestamoId"),
                            FechaPago = rPagos.GetDateTime("FechaPago"),
                            Periodo = rPagos.GetString("Periodo"),
                            MontoInteres = rPagos.GetDecimal("MontoInteres"),
                            MontoMora = rPagos.GetDecimal("MontoMora"),
                            MontoCapital = rPagos.GetDecimal("MontoCapital"),
                            TotalPagado = rPagos.GetDecimal("TotalPagado"),
                            TipoPago = rPagos.GetString("TipoPago"),
                            Frecuencia = rPagos.GetString("Frecuencia"),
                            Observacion = rPagos.IsDBNull(rPagos.GetOrdinal("Observacion")) ? null : rPagos.GetString("Observacion")
                        });
                    }
                }
            }

            return View(lista);
        }

        // =====================================================
        // AMPLIAR (Reenganchar)
        // =====================================================
        [HttpGet]
        public IActionResult Ampliar(int id)
        {
            using var con = _db.GetConn();
            con.Open();

            string sql = @"SELECT p.Id, p.MontoCapital, p.MontoOriginal, c.NombreCompleto
                   FROM Prestamos p
                   INNER JOIN Clientes c ON p.ClienteId = c.Id
                   WHERE p.Id = @id";
            using var cmd = new MySqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", id);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return NotFound();

            var model = new AmpliacionViewModel
            {
                PrestamoId = r.GetInt32("Id"),
                ClienteNombre = r.GetString("NombreCompleto"),
                SaldoActual = r.GetDecimal("MontoCapital"),
                MontoOriginal = r.GetDecimal("MontoOriginal")
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult Ampliar(AmpliacionViewModel model)
        {
            if (model.MontoAmpliado <= 0)
            {
                ModelState.AddModelError("MontoAmpliado", "Debe ingresar un monto válido.");
                return View(model);
            }

            using var con = _db.GetConn();
            con.Open();

            // 1. Insertar historial de ampliaciones
            string sqlHist = @"INSERT INTO Ampliaciones (PrestamoId, MontoAmpliado, Observacion)
                       VALUES (@p, @m, @o)";
            using (var cmdHist = new MySqlCommand(sqlHist, con))
            {
                cmdHist.Parameters.AddWithValue("@p", model.PrestamoId);
                cmdHist.Parameters.AddWithValue("@m", model.MontoAmpliado);
                cmdHist.Parameters.AddWithValue("@o", model.Observacion ?? (object)DBNull.Value);
                cmdHist.ExecuteNonQuery();
            }

            // 2. ✅ Actualizar préstamo → solo capital (NO el monto original)
            string sqlPrestamo = @"UPDATE Prestamos 
                           SET MontoCapital = MontoCapital + @m
                           WHERE Id=@id";
            using (var cmdPrestamo = new MySqlCommand(sqlPrestamo, con))
            {
                cmdPrestamo.Parameters.AddWithValue("@m", model.MontoAmpliado);
                cmdPrestamo.Parameters.AddWithValue("@id", model.PrestamoId);
                cmdPrestamo.ExecuteNonQuery();
            }

            // 3. Obtener info del préstamo
            string sqlInfo = @"SELECT EsPlazoDefinido, NumeroMeses, PorcentajeMensual, DiaPago
                       FROM Prestamos WHERE Id=@id";
            using var cmdInfo = new MySqlCommand(sqlInfo, con);
            cmdInfo.Parameters.AddWithValue("@id", model.PrestamoId);

            using var rInfo = cmdInfo.ExecuteReader();
            if (rInfo.Read() && rInfo.GetBoolean("EsPlazoDefinido") && !rInfo.IsDBNull(rInfo.GetOrdinal("NumeroMeses")))
            {
                int meses = rInfo.GetInt32("NumeroMeses");
                DateTime diaPago = rInfo.GetDateTime("DiaPago");
                rInfo.Close();

                // 🔹 4. Calcular saldo pendiente real (cuotas no pagadas aún)
                decimal saldoPendiente = 0;
                string sqlSaldo = @"SELECT IFNULL(SUM(MontoCapital),0) 
                            FROM Cuotas 
                            WHERE PrestamoId=@p AND Estado IN ('Pendiente','Vencida')";
                using (var cmdSaldo = new MySqlCommand(sqlSaldo, con))
                {
                    cmdSaldo.Parameters.AddWithValue("@p", model.PrestamoId);
                    saldoPendiente = Convert.ToDecimal(cmdSaldo.ExecuteScalar() ?? 0);
                }

                // 🔹 5. Cancelar esas cuotas pendientes y vencidas
                string sqlCancelarCuotas = @"UPDATE Cuotas 
                                     SET Estado='Cancelada' 
                                     WHERE PrestamoId=@p AND Estado IN ('Pendiente','Vencida')";
                using (var cmdCancel = new MySqlCommand(sqlCancelarCuotas, con))
                {
                    cmdCancel.Parameters.AddWithValue("@p", model.PrestamoId);
                    cmdCancel.ExecuteNonQuery();
                }

                // 🔹 6. Nuevo capital = saldo pendiente + ampliación
                decimal nuevoCapital = saldoPendiente + model.MontoAmpliado;
                decimal montoCuotaCapital = nuevoCapital / meses;

                // 🔹 7. Obtener el interés original de la primera cuota
                decimal interesFijo = 0;
                string sqlInteres = @"SELECT MontoInteres 
                              FROM Cuotas 
                              WHERE PrestamoId=@p AND EsReenganche=0 
                              ORDER BY NumeroCuota ASC LIMIT 1";
                using (var cmdInteres = new MySqlCommand(sqlInteres, con))
                {
                    cmdInteres.Parameters.AddWithValue("@p", model.PrestamoId);
                    var result = cmdInteres.ExecuteScalar();
                    interesFijo = result != null ? Convert.ToDecimal(result) : 0;
                }

                // 🔹 8. Generar nuevas cuotas de reenganche
                for (int i = 1; i <= meses; i++)
                {
                    DateTime fechaVencimiento = diaPago.AddMonths(i - 1);

                    string sqlCuota = @"INSERT INTO Cuotas
                                (PrestamoId, NumeroCuota, FechaVencimiento,
                                 MontoCapital, MontoInteres, MontoTotal,
                                 Estado, EsReenganche)
                                VALUES (@p, @n, @f, @mc, @mi, @mt, 'Pendiente', 1)";
                    using var cmdCuota = new MySqlCommand(sqlCuota, con);
                    cmdCuota.Parameters.AddWithValue("@p", model.PrestamoId);
                    cmdCuota.Parameters.AddWithValue("@n", i);
                    cmdCuota.Parameters.AddWithValue("@f", fechaVencimiento);

                    // ✅ Mantener siempre el interés original fijo
                    decimal interes = interesFijo;

                    cmdCuota.Parameters.AddWithValue("@mc", montoCuotaCapital);
                    cmdCuota.Parameters.AddWithValue("@mi", interes);
                    cmdCuota.Parameters.AddWithValue("@mt", montoCuotaCapital + interes);

                    cmdCuota.ExecuteNonQuery();
                }
            }
            else
            {
                rInfo.Close();
            }

            TempData["Mensaje"] = "✅ Préstamo reenganchado correctamente.";
            return RedirectToAction(nameof(Index));
        }



        // =====================================================
        // CREATE
        // =====================================================
        [HttpGet]
        public IActionResult Create()
        {
            // 🔹 Llenar dropdown de clientes
            var clientes = new List<Cliente>();
            using var con = _db.GetConn();
            con.Open();
            string sql = "SELECT Id, NombreCompleto FROM Clientes WHERE Estado=1 ORDER BY NombreCompleto";
            using var cmd = new MySqlCommand(sql, con);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                clientes.Add(new Cliente
                {
                    Id = r.GetInt32("Id"),
                    NombreCompleto = r.GetString("NombreCompleto")
                });
            }
            ViewBag.Clientes = clientes;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Prestamo model)
        {
            if (!ModelState.IsValid)
            {
                // 🔹 Recargar clientes si falla validación
                ViewBag.Clientes = ObtenerClientes();
                return View(model);
            }

            using var con = _db.GetConn();
            con.Open();

            string sql = @"INSERT INTO Prestamos 
       (ClienteId, MontoCapital, MontoOriginal, PorcentajeMensual, 
        FechaDesembolso, DiaPago, MoraFija, Estado, EsPlazoDefinido, NumeroMeses, Observacion)
       VALUES (@c,@mc,@mo,@pm,@fd,@dp,@mf,'Activo',@ep,@nm,@o)";
            using var cmd = new MySqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@c", model.ClienteId);
            cmd.Parameters.AddWithValue("@mc", model.MontoCapital);
            cmd.Parameters.AddWithValue("@mo", model.MontoCapital); // ✅ Guardamos también como MontoOriginal
            cmd.Parameters.AddWithValue("@pm", model.PorcentajeMensual);
            cmd.Parameters.AddWithValue("@fd", model.FechaDesembolso);
            cmd.Parameters.AddWithValue("@dp", model.DiaPago);
            cmd.Parameters.AddWithValue("@mf", model.MoraFija);
            cmd.Parameters.AddWithValue("@ep", model.EsPlazoDefinido);
            cmd.Parameters.AddWithValue("@nm", model.NumeroMeses ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@o", model.Observacion ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();

            // =====================================================
            // Generar cuotas automáticamente si es a plazo definido
            // =====================================================
            if (model.EsPlazoDefinido && model.NumeroMeses.HasValue)
            {
                int prestamoId;
                using (var cmdLastId = new MySqlCommand("SELECT LAST_INSERT_ID();", con))
                {
                    prestamoId = Convert.ToInt32(cmdLastId.ExecuteScalar());
                }

                decimal montoCapital = model.MontoCapital;
                int meses = model.NumeroMeses.Value;
                decimal montoCuotaCapital = montoCapital / meses;

                for (int i = 1; i <= meses; i++)
                {
                    DateTime fechaVencimiento = model.DiaPago.AddMonths(i - 1);

                    string sqlCuota = @"INSERT INTO Cuotas
                (PrestamoId, NumeroCuota, FechaVencimiento,
                 MontoCapital, MontoInteres, MontoTotal,
                 Estado, EsReenganche)
                VALUES (@p, @n, @f, @mc, @mi, @mt, 'Pendiente', 0)";
                    using var cmdCuota = new MySqlCommand(sqlCuota, con);
                    cmdCuota.Parameters.AddWithValue("@p", prestamoId);
                    cmdCuota.Parameters.AddWithValue("@n", i);
                    cmdCuota.Parameters.AddWithValue("@f", fechaVencimiento);

                    // ✅ Interés calculado siempre sobre el monto original completo
                    // ✅ Interés calculado sobre el monto total del préstamo
                    decimal interes = (model.MontoCapital * model.PorcentajeMensual) / 100m;


                    cmdCuota.Parameters.AddWithValue("@mc", montoCuotaCapital);
                    cmdCuota.Parameters.AddWithValue("@mi", interes);
                    cmdCuota.Parameters.AddWithValue("@mt", montoCuotaCapital + interes);

                    cmdCuota.ExecuteNonQuery();
                }
            }

            TempData["Mensaje"] = "✅ Préstamo creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        private List<Cliente> ObtenerClientes()
        {
            var clientes = new List<Cliente>();
            using var con = _db.GetConn();
            con.Open();
            string sql = "SELECT Id, NombreCompleto FROM Clientes WHERE Estado=1 ORDER BY NombreCompleto";
            using var cmd = new MySqlCommand(sql, con);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                clientes.Add(new Cliente
                {
                    Id = r.GetInt32("Id"),
                    NombreCompleto = r.GetString("NombreCompleto")
                });
            }
            return clientes;
        }

        [HttpGet]
        public IActionResult Recibo(int id)
        {
            using var con = _db.GetConn();
            con.Open();

            // ─────────────────────────────────────────────────────────────
            // 1) Préstamo + cliente
            // ─────────────────────────────────────────────────────────────
            string sqlPago = @"
SELECT p.Id AS PagoId, p.FechaPago, p.Periodo, p.MontoMora, 
       p.TotalPagado, p.TipoPago, p.Frecuencia, p.Observacion,
       c.Id AS CuotaId, c.NumeroCuota, c.FechaVencimiento, 
       c.MontoCapital, c.MontoInteres, c.EsReenganche,
       pr.Id AS PrestamoId, pr.MontoOriginal, pr.PorcentajeMensual, cl.NombreCompleto,
(SELECT IFNULL(SUM(pg.MontoCapital),0) 
 FROM Pagos pg 
 WHERE pg.PrestamoId = pr.Id) AS CapitalPagado

FROM Pagos p
LEFT JOIN Cuotas c ON c.Id = p.CuotaId
INNER JOIN Prestamos pr ON pr.Id = p.PrestamoId
INNER JOIN Clientes cl ON cl.Id = pr.ClienteId
WHERE p.Id = @id";

            using var cmdP = new MySqlCommand(sqlPago, con);
            cmdP.Parameters.AddWithValue("@id", id);

            int prestamoId = 0, cuotaId = 0, numeroCuota = 0;
            string cliente = null, tipoPago = null, periodo = null, frecuencia = null, observacion = null;
            decimal montoOriginal = 0, saldoActual = 0, porcMensual = 0;
            decimal cuotaCapital = 0, cuotaInteres = 0, cuotaTotal = 0, pagoMora = 0m, pagoTotal = 0m;
            decimal capitalPagado = 0m, totalAmpliado = 0m, nuevoCapital = 0m;  // ✅ declarados solo una vez
            DateTime fechaVenc = DateTime.MinValue;
            DateTime? fechaPago = null;
            bool tieneCuota = false;
            bool esReengancheBD = false;

            // Variables de control
            bool esNormal = false;
            bool esReengancheFinal = false;
            bool esOriginal = false;

            using (var rp = cmdP.ExecuteReader())
            {
                if (!rp.Read()) return NotFound();

                // Préstamo
                prestamoId = rp.GetInt32("PrestamoId");
                montoOriginal = rp.GetDecimal("MontoOriginal");
                capitalPagado = rp.GetDecimal("CapitalPagado");   // ✅ solo asigna
                porcMensual = rp.GetDecimal("PorcentajeMensual");
                cliente = rp.GetString("NombreCompleto");

                // Cuota (si existe)
                if (!rp.IsDBNull(rp.GetOrdinal("CuotaId")))
                {
                    tieneCuota = true;
                    cuotaId = rp.GetInt32("CuotaId");
                    numeroCuota = rp.GetInt32("NumeroCuota");
                    fechaVenc = rp.GetDateTime("FechaVencimiento");
                    cuotaCapital = rp.GetDecimal("MontoCapital");
                    cuotaInteres = rp.GetDecimal("MontoInteres");
                    cuotaTotal = cuotaCapital + cuotaInteres;
                    esReengancheBD = rp.GetBoolean("EsReenganche");
                }
                else
                {
                    tieneCuota = false; // Es un pago normal
                }

                // Pago
                fechaPago = rp.GetDateTime("FechaPago");
                pagoMora = rp.GetDecimal("MontoMora");
                pagoTotal = rp.GetDecimal("TotalPagado");
                tipoPago = rp.IsDBNull(rp.GetOrdinal("TipoPago")) ? null : rp.GetString("TipoPago");
                periodo = rp.IsDBNull(rp.GetOrdinal("Periodo")) ? null : rp.GetString("Periodo");
                frecuencia = rp.IsDBNull(rp.GetOrdinal("Frecuencia")) ? null : rp.GetString("Frecuencia");
                observacion = rp.IsDBNull(rp.GetOrdinal("Observacion")) ? null : rp.GetString("Observacion");

                esNormal = !tieneCuota;
                esReengancheFinal = tieneCuota && esReengancheBD;
                esOriginal = tieneCuota && !esReengancheBD;
            }

            // 🔹 Sumar ampliaciones (reenganche) al capital original
            string sqlAmp = @"SELECT IFNULL(SUM(MontoAmpliado),0) 
                  FROM Ampliaciones 
                  WHERE PrestamoId=@id";
            using var cmdAmp = new MySqlCommand(sqlAmp, con);
            cmdAmp.Parameters.AddWithValue("@id", prestamoId);
            totalAmpliado = Convert.ToDecimal(cmdAmp.ExecuteScalar() ?? 0);

            nuevoCapital = montoOriginal + totalAmpliado;

            // 🔹 Calcular saldo real
            saldoActual = nuevoCapital - capitalPagado;

            // 🔹 Evitar residuales negativos (por redondeos o pagos exactos)
            if (saldoActual < 0.5m) saldoActual = 0;




            // ─────────────────────────────────────────────────────────────
            // 4) Próxima cuota
            // ─────────────────────────────────────────────────────────────
            int? proxNum = null; DateTime? proxFecha = null; decimal? proxMonto = null;
            string sqlNext = @"
SELECT NumeroCuota, FechaVencimiento, MontoTotal
FROM Cuotas
WHERE PrestamoId=@p AND Estado IN ('Pendiente','Vencida')
ORDER BY FechaVencimiento ASC, NumeroCuota ASC
LIMIT 1";
            using var cmdNext = new MySqlCommand(sqlNext, con);
            cmdNext.Parameters.AddWithValue("@p", prestamoId);
            using (var rn = cmdNext.ExecuteReader())
            {
                if (rn.Read())
                {
                    proxNum = rn.GetInt32("NumeroCuota");
                    proxFecha = rn.GetDateTime("FechaVencimiento");
                    proxMonto = rn.GetDecimal("MontoTotal");
                }
            }

            // ─────────────────────────────────────────────────────────────
            // 5) PDF (diseño moderno y explicativo)
            // ─────────────────────────────────────────────────────────────
            string brand = "#5B21B6";
            string brand2 = "#7C3AED";
            string ink = "#111827";
            string gray700 = "#374151";
            string gray500 = "#6B7280";
            string gray200 = "#E5E7EB";
            string gray100 = "#F3F4F6";
            string paper = "#FFFFFF";
            string warn = "#DC2626";
            string success = "#16A34A";

            var stream = new MemoryStream();

            Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Margin(34);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(t => t.FontSize(11).FontColor(ink));

                    // HEADER
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Background(brand).Padding(14).Column(c =>
                        {
                            c.Item().Text("PrestamistaRD").FontColor("#fff").Bold();
                            c.Item().Text(esReengancheFinal ? "RECIBO DE REENGANCHE" : "RECIBO DE PAGO")
                                    .FontColor("#fff").FontSize(22).Bold();
                        });
                        row.ConstantItem(210).Background(brand2).Padding(12).Column(c =>
                        {
                            c.Item().AlignRight().Text($"Préstamo # {prestamoId}").FontColor("#fff");
                            c.Item().AlignRight().Text($"Emitido: {(fechaPago ?? DateTime.Now):dd/MM/yyyy}").FontColor("#fff");
                        });
                    });

                    // CONTENT
                    page.Content().PaddingVertical(18).Column(col =>
                    {
                        col.Spacing(18);

                        // Datos del cliente y préstamo
                        col.Item().Background(gray100).Border(1).BorderColor(gray200).Padding(14).Column(b =>
                        {
                            b.Spacing(6);

                            b.Item().Row(r =>
                            {
                                r.RelativeItem().Column(x =>
                                {
                                    x.Item().Text("Cliente").FontColor(gray500);
                                    x.Item().Text(cliente).Bold().FontSize(14);
                                });
                                r.ConstantItem(230).Column(x =>
                                {
                                    x.Item().AlignRight().Text(esNormal ? "Plan: Pago directo (sin cuota)" : esReengancheFinal ? "Plan: Reenganche (cuotas recalculadas)" : "Plan: A plazo (cuotas originales)").FontColor(gray700);
                                    x.Item().AlignRight().Text($"Interés mensual: {porcMensual:N2} %").FontColor(gray700);
                                });
                            });

                            if (totalAmpliado > 0) // Con reenganche
                            {
                                b.Item().Row(r =>
                                {
                                    r.RelativeItem().Background(paper).Border(1).BorderColor(gray200).Padding(12).Column(x =>
                                    {
                                        x.Item().Text("Monto original").FontColor(gray500);
                                        x.Item().Text($"RD$ {montoOriginal:N2}");
                                    });
                                    r.RelativeItem().Background(paper).Border(1).BorderColor(gray200).Padding(12).Column(x =>
                                    {
                                        x.Item().Text("Ampliación total").FontColor(gray500);
                                        x.Item().Text($"RD$ {totalAmpliado:N2}");
                                    });
                                    r.RelativeItem().Background(paper).Border(1).BorderColor(gray200).Padding(12).Column(x =>
                                    {
                                        x.Item().Text("Nuevo capital").FontColor(gray500);
                                        x.Item().Text($"RD$ {nuevoCapital:N2}").Bold().FontColor(brand);
                                    });
                                });
                            }
                            else // Sin reenganche
                            {
                                b.Item().Row(r =>
                                {
                                    r.RelativeItem().Background(paper).Border(1).BorderColor(gray200).Padding(12).Column(x =>
                                    {
                                        x.Item().Text("Monto original").FontColor(gray500);
                                        x.Item().Text($"RD$ {montoOriginal:N2}").Bold().FontColor(brand);
                                    });
                                    r.RelativeItem().Background(paper).Border(1).BorderColor(gray200).Padding(12).Column(x =>
                                    {
                                        x.Item().Text("Saldo pendiente").FontColor(gray500);
                                        x.Item().Text($"RD$ {saldoActual:N2}").Bold().FontColor(brand);
                                    });
                                });
                            }
                        });

                        // Información de cuota o pago normal
                        if (tieneCuota)
                        {
                            col.Item().Background("#EEF2FF").Border(1).BorderColor("#CBD5E1").Padding(14).Column(b =>
                            {
                                b.Spacing(6);
                                b.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Información de la cuota").Bold();
                                    r.ConstantItem(160).AlignRight().Text(esReengancheFinal ? "Tipo: Recalculada por Reenganche" : "Tipo: Original").FontColor(esReengancheFinal ? warn : success).Bold();                                                        
                                });
                                b.Item().Row(r =>
                                {
                                    r.RelativeItem().Text($"Cuota Nº {numeroCuota}");
                                    r.ConstantItem(220).AlignRight().Text($"Vencimiento: {fechaVenc:dd/MM/yyyy}");
                                });
                                b.Item().Text("Estado: Pagada").FontColor(success);
                            });
                        }
                        else
                        {
                            // 🔹 Sección para pagos normales (sin cuota)
                            col.Item().Background("#EEF2FF").Border(1).BorderColor("#CBD5E1").Padding(14).Column(b =>
                            {
                                b.Spacing(6);
                                b.Item().Text("Información del pago normal").Bold();
                                b.Item().Text($"Total pagado: RD$ {pagoTotal:N2}").FontColor(success).Bold();
                                if (fechaPago is not null)
                                    b.Item().Text($"Fecha de pago: {((DateTime)fechaPago):dd/MM/yyyy}");
                            });
                        }


                        // Desglose de pago
                        col.Item().Border(1).BorderColor(gray200).Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(1);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Background(brand).Padding(8).Text("Concepto").FontColor("#fff").Bold();
                                h.Cell().Background(brand2).Padding(8).Text("Monto").FontColor("#fff").Bold();
                            });

                            void Row(string cpt, string monto)
                            {
                                t.Cell().PaddingVertical(8).PaddingHorizontal(10).BorderBottom(1).BorderColor(gray200).Text(cpt);
                                t.Cell().PaddingVertical(8).PaddingHorizontal(10).BorderBottom(1).BorderColor(gray200).AlignRight().Text(monto).Bold();
                            }

                            Row("Capital de la cuota", $"RD$ {cuotaCapital:N2}");
                            Row("Interés de la cuota", $"RD$ {cuotaInteres:N2}");
                            Row("Mora aplicada", $"RD$ {pagoMora:N2}");
                            Row("Total pagado", $"RD$ {pagoTotal:N2}");
                        });

                        // Datos del pago
                        if (fechaPago is not null || tipoPago is not null || periodo is not null || frecuencia is not null || !string.IsNullOrWhiteSpace(observacion))
                        {
                            col.Item().Background(paper).Border(1).BorderColor(gray200).Padding(12).Column(b =>
                            {
                                b.Spacing(4);
                                b.Item().Text("Detalles del pago").Bold().FontColor(gray700);
                                if (fechaPago is not null) b.Item().Text($"Fecha de pago: {((DateTime)fechaPago):dd/MM/yyyy}");
                                if (!string.IsNullOrWhiteSpace(tipoPago)) b.Item().Text($"Tipo de pago: {tipoPago}");
                                if (!string.IsNullOrWhiteSpace(periodo)) b.Item().Text($"Periodo: {periodo}");
                                if (!string.IsNullOrWhiteSpace(frecuencia)) b.Item().Text($"Frecuencia: {frecuencia}");
                                if (!string.IsNullOrWhiteSpace(observacion)) b.Item().Text($"Observación: {observacion}");
                            });
                        }

                        // Estado posterior
                        col.Item().Background(gray100).Border(1).BorderColor(gray200).Padding(12).Row(r =>
                        {
                            r.RelativeItem().Column(x =>
                            {
                                x.Item().Text("Estado del préstamo después del pago").Bold().FontColor(gray700);
                                x.Item().Text($"Saldo pendiente: RD$ {saldoActual:N2}");
                            });
                            r.ConstantItem(260).Column(x =>
                            {
                                if (proxNum is not null)
                                {
                                    x.Item().AlignRight().Text("Próxima cuota").Bold();
                                    x.Item().AlignRight().Text($"Nº {proxNum}  •  {proxFecha:dd/MM/yyyy}");
                                    x.Item().AlignRight().Text($"Monto: RD$ {proxMonto:N2}");
                                }
                                else
                                {
                                    x.Item().AlignRight().Text("No hay cuotas pendientes").FontColor(success);
                                }
                            });
                        });

                        // Nota aclaratoria
                        if (esReengancheFinal)
                        {
                            col.Item().PaddingTop(8).Text(
                                "Nota: Esta cuota fue recalculada como parte de un REENGANCHE. " +
                                "El capital del préstamo se actualizó y se generó un nuevo plan de cuotas."
                            ).FontSize(9).FontColor(gray500).Italic();
                        }

                    });

                    // FOOTER
                    page.Footer().Column(f =>
                    {
                        f.Item().LineHorizontal(1).LineColor(gray200);
                        f.Item().PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Text("PrestamistaRD • Documento generado automáticamente").FontColor(gray500).FontSize(9);
                            r.ConstantItem(230).AlignRight().Text("Firma autorizada: ______________________").FontColor(gray700).FontSize(10);
                        });
                    });
                });
            }).GeneratePdf(stream);

            var nombre = esReengancheFinal ? $"Recibo_Reenganche_{prestamoId}.pdf" : $"Recibo_Pago_{prestamoId}.pdf";
            return File(stream.ToArray(), "application/pdf", nombre);
        }

        [HttpGet]
        public IActionResult ReciboNormal(int id)
        {
            using var con = _db.GetConn();
            con.Open();

            string sql = @"
    SELECT p.*, c.NombreCompleto, pr.MontoOriginal, pr.PorcentajeMensual,
           (pr.MontoOriginal - (SELECT IFNULL(SUM(pg.MontoCapital), 0) 
                                FROM Pagos pg 
                                WHERE pg.PrestamoId = pr.Id)) AS SaldoPendiente
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
                TipoPago = r.IsDBNull(r.GetOrdinal("TipoPago")) ? null : r.GetString("TipoPago"),
                Periodo = r.IsDBNull(r.GetOrdinal("Periodo")) ? null : r.GetString("Periodo"),
                Frecuencia = r.IsDBNull(r.GetOrdinal("Frecuencia")) ? null : r.GetString("Frecuencia"),
                MontoInteres = r.GetDecimal("MontoInteres"),
                MontoMora = r.GetDecimal("MontoMora"),
                MontoCapital = r.GetDecimal("MontoCapital"),
                TotalPagado = r.GetDecimal("TotalPagado"),
                Observacion = r.IsDBNull(r.GetOrdinal("Observacion")) ? null : r.GetString("Observacion")
            };

            string cliente = r.GetString("NombreCompleto");
            decimal montoOriginal = r.GetDecimal("MontoOriginal");
            decimal saldoPendiente = r.GetDecimal("SaldoPendiente");
            decimal interes = r.GetDecimal("PorcentajeMensual");


            r.Close();

            // Generar recibo en PDF
            return GenerarReciboNormal(pago, cliente, montoOriginal, saldoPendiente, interes);
        }


        private FileResult GenerarReciboNormal(Pago pago, string cliente, decimal montoOriginal, decimal saldoActual, decimal interes)
        {
            string brand = "#5B21B6";
            string brand2 = "#7C3AED";
            string ink = "#111827";
            string gray700 = "#374151";
            string gray500 = "#6B7280";
            string gray200 = "#E5E7EB";
            string gray100 = "#F3F4F6";
            string paper = "#FFFFFF";
            string success = "#16A34A";

            var stream = new MemoryStream();

            Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Margin(34);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(t => t.FontSize(11).FontColor(ink));

                    // HEADER
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Background(brand).Padding(14).Column(c =>
                        {
                            c.Item().Text("PrestamistaRD").FontColor("#fff").Bold();
                            c.Item().Text("RECIBO DE PAGO").FontColor("#fff").FontSize(22).Bold();
                        });
                        row.ConstantItem(210).Background(brand2).Padding(12).Column(c =>
                        {
                            c.Item().AlignRight().Text($"Préstamo # {pago.PrestamoId}").FontColor("#fff");
                            c.Item().AlignRight().Text($"Emitido: {DateTime.Now:dd/MM/yyyy}").FontColor("#fff");
                        });
                    });

                    // CONTENT
                    page.Content().PaddingVertical(18).Column(col =>
                    {
                        col.Spacing(18);

                        // Datos del cliente y préstamo
                        col.Item().Background(gray100).Border(1).BorderColor(gray200).Padding(14).Column(b =>
                        {
                            b.Spacing(6);

                            b.Item().Row(r =>
                            {
                                r.RelativeItem().Column(x =>
                                {
                                    x.Item().Text("Cliente").FontColor(gray500);
                                    x.Item().Text(cliente).Bold().FontSize(14);
                                });
                                r.ConstantItem(230).Column(x =>
                                {
                                    x.Item().AlignRight().Text("Plan: Pago Directo (sin cuotas)").FontColor(gray700);
                                    x.Item().AlignRight().Text($"Interés mensual: {interes:N2} %").FontColor(gray700);
                                });
                            });

                            b.Item().Row(r =>
                            {
                                r.RelativeItem().Background(paper).Border(1).BorderColor(gray200).Padding(12).Column(x =>
                                {
                                    x.Item().Text("Monto original").FontColor(gray500);
                                    x.Item().Text($"RD$ {montoOriginal:N2}").Bold().FontColor(brand);
                                });
                                r.RelativeItem().Background(paper).Border(1).BorderColor(gray200).Padding(12).Column(x =>
                                {
                                    x.Item().Text("Saldo pendiente").FontColor(gray500);
                                    x.Item().Text($"RD$ {saldoActual:N2}").Bold().FontColor(brand);
                                });
                            });

                        });

                        // Información del pago directo
                        col.Item().Background("#EEF2FF").Border(1).BorderColor("#CBD5E1").Padding(14).Column(b =>
                        {
                            b.Spacing(6);
                            b.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Información del pago normal").Bold();
                                r.ConstantItem(160).AlignRight().Text("Tipo: Directo").FontColor(success).Bold();
                            });
                            b.Item().Text($"Estado: Pagado").FontColor(success);
                        });

                        // Desglose del pago
                        col.Item().Border(1).BorderColor(gray200).Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(1);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Background(brand).Padding(8).Text("Concepto").FontColor("#fff").Bold();
                                h.Cell().Background(brand2).Padding(8).Text("Monto").FontColor("#fff").Bold();
                            });

                            void Row(string cpt, string monto)
                            {
                                t.Cell().PaddingVertical(8).PaddingHorizontal(10).BorderBottom(1).BorderColor(gray200).Text(cpt);
                                t.Cell().PaddingVertical(8).PaddingHorizontal(10).BorderBottom(1).BorderColor(gray200).AlignRight().Text(monto).Bold();
                            }

                            Row("Capital pagado", $"RD$ {pago.MontoCapital:N2}");
                            Row("Interés pagado", $"RD$ {pago.MontoInteres:N2}");
                            Row("Mora aplicada", $"RD$ {pago.MontoMora:N2}");
                            Row("Total pagado", $"RD$ {pago.TotalPagado:N2}");
                        });

                        // Datos del pago
                        col.Item().Background(paper).Border(1).BorderColor(gray200).Padding(12).Column(b =>
                        {
                            b.Spacing(4);
                            b.Item().Text("Detalles del pago").Bold().FontColor(gray700);
                            b.Item().Text($"Fecha de pago: {pago.FechaPago:dd/MM/yyyy}");
                            if (!string.IsNullOrWhiteSpace(pago.TipoPago)) b.Item().Text($"Tipo de pago: {pago.TipoPago}");
                            if (!string.IsNullOrWhiteSpace(pago.Periodo)) b.Item().Text($"Periodo: {pago.Periodo}");
                            if (!string.IsNullOrWhiteSpace(pago.Frecuencia)) b.Item().Text($"Frecuencia: {pago.Frecuencia}");
                            if (!string.IsNullOrWhiteSpace(pago.Observacion)) b.Item().Text($"Observación: {pago.Observacion}");
                        });

                        // Estado posterior
                        col.Item().Background(gray100).Border(1).BorderColor(gray200).Padding(12).Row(r =>
                        {
                            r.RelativeItem().Column(x =>
                            {
                                x.Item().Text("Estado del préstamo después del pago").Bold().FontColor(gray700);
                                x.Item().Text($"Saldo pendiente: RD$ {saldoActual:N2}");
                            });
                        });
                    });

                    // FOOTER
                    page.Footer().Column(f =>
                    {
                        f.Item().LineHorizontal(1).LineColor(gray200);
                        f.Item().PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Text("PrestamistaRD • Documento generado automáticamente").FontColor(gray500).FontSize(9);
                            r.ConstantItem(230).AlignRight().Text("Firma autorizada: ______________________").FontColor(gray700).FontSize(10);
                        });
                    });
                });
            }).GeneratePdf(stream);

            return File(stream.ToArray(), "application/pdf", $"Recibo_Normal_{pago.Id}.pdf");
        }


        // =====================================================
        // DELETE
        // =====================================================
        [HttpGet]
        public IActionResult Delete(int id)
        {
            using var con = _db.GetConn();
            con.Open();

            // Eliminar cuotas
            string sqlCuotas = "DELETE FROM Cuotas WHERE PrestamoId=@id";
            using (var cmdC = new MySqlCommand(sqlCuotas, con))
            {
                cmdC.Parameters.AddWithValue("@id", id);
                cmdC.ExecuteNonQuery();
            }

            // Eliminar pagos
            string sqlPagos = "DELETE FROM Pagos WHERE PrestamoId=@id";
            using (var cmdP = new MySqlCommand(sqlPagos, con))
            {
                cmdP.Parameters.AddWithValue("@id", id);
                cmdP.ExecuteNonQuery();
            }

            // Eliminar ampliaciones
            string sqlAmp = "DELETE FROM Ampliaciones WHERE PrestamoId=@id";
            using (var cmdA = new MySqlCommand(sqlAmp, con))
            {
                cmdA.Parameters.AddWithValue("@id", id);
                cmdA.ExecuteNonQuery();
            }

            // Eliminar préstamo
            string sqlPrestamo = "DELETE FROM Prestamos WHERE Id=@id";
            using (var cmd = new MySqlCommand(sqlPrestamo, con))
            {
                cmd.Parameters.AddWithValue("@id", id);
                int filas = cmd.ExecuteNonQuery();

                if (filas > 0)
                {
                    TempData["Mensaje"] = "✅ Préstamo eliminado correctamente.";
                }
                else
                {
                    TempData["Mensaje"] = "❌ No se encontró el préstamo.";
                }
            }

            return RedirectToAction(nameof(Index));
        }
    }

}