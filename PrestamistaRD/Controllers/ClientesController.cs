using Microsoft.AspNetCore.Mvc;
using PrestamistaRD.Data;
using PrestamistaRD.Models;
using MySql.Data.MySqlClient;

namespace PrestamistaRD.Controllers
{
    /// <summary>
    /// Controlador para la gestión de clientes.
    /// Permite realizar operaciones CRUD (Crear, Leer, Actualizar, Eliminar).
    /// </summary>
    public class ClientesController : Controller
    {
        private readonly Db _db;
        public ClientesController(Db db) => _db = db;

        /// <summary>
        /// Muestra la lista de todos los clientes.
        /// </summary>
        public IActionResult Index()
        {
            var lista = new List<Cliente>();
            using var con = _db.GetConn();
            con.Open();
            using var cmd = new MySqlCommand("SELECT * FROM Clientes", con);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new Cliente
                {
                    Id = r.GetInt32("Id"),
                    NombreCompleto = r.GetString("NombreCompleto"),
                    Cedula = r.GetString("Cedula"),
                    Telefono = r.GetString("Telefono"),
                    Direccion = r.GetString("Direccion"),
                    Estado = r.GetBoolean("Estado")
                });
            }
            return View(lista);
        }

        /// <summary>
        /// Devuelve la vista de creación de un nuevo cliente.
        /// </summary>
        public IActionResult Create() => View();

        /// <summary>
        /// Procesa la creación de un nuevo cliente.
        /// </summary>
        [HttpPost]
        public IActionResult Create(Cliente model)
        {
            if (!ModelState.IsValid) return View(model);

            using var con = _db.GetConn();
            con.Open();
            using var cmd = new MySqlCommand(@"INSERT INTO Clientes 
                (NombreCompleto,Cedula,Telefono,Direccion,Estado) 
                VALUES (@n,@c,@t,@d,1)", con);

            cmd.Parameters.AddWithValue("@n", model.NombreCompleto);
            cmd.Parameters.AddWithValue("@c", model.Cedula);
            cmd.Parameters.AddWithValue("@t", model.Telefono);
            cmd.Parameters.AddWithValue("@d", model.Direccion);
            cmd.ExecuteNonQuery();

            TempData["Mensaje"] = "Cliente agregado correctamente";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Carga la vista de edición con los datos de un cliente existente.
        /// </summary>
        public IActionResult Edit(int id)
        {
            using var con = _db.GetConn();
            con.Open();
            using var cmd = new MySqlCommand("SELECT * FROM Clientes WHERE Id=@id", con);
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return NotFound();

            var c = new Cliente
            {
                Id = r.GetInt32("Id"),
                NombreCompleto = r.GetString("NombreCompleto"),
                Cedula = r.GetString("Cedula"),
                Telefono = r.GetString("Telefono"),
                Direccion = r.GetString("Direccion"),
                Estado = r.GetBoolean("Estado")
            };
            return View(c);
        }

        /// <summary>
        /// Procesa la actualización de los datos de un cliente.
        /// </summary>
        [HttpPost]
        public IActionResult Edit(Cliente model)
        {
            if (!ModelState.IsValid) return View(model);

            using var con = _db.GetConn();
            con.Open();
            using var cmd = new MySqlCommand(@"UPDATE Clientes 
                SET NombreCompleto=@n, Cedula=@c, Telefono=@t, Direccion=@d, Estado=@e 
                WHERE Id=@id", con);

            cmd.Parameters.AddWithValue("@n", model.NombreCompleto);
            cmd.Parameters.AddWithValue("@c", model.Cedula);
            cmd.Parameters.AddWithValue("@t", model.Telefono);
            cmd.Parameters.AddWithValue("@d", model.Direccion);
            cmd.Parameters.AddWithValue("@e", model.Estado);
            cmd.Parameters.AddWithValue("@id", model.Id);
            cmd.ExecuteNonQuery();

            TempData["Mensaje"] = "Cliente actualizado correctamente";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Elimina un cliente por Id (valida FK con préstamos).
        /// </summary>
        public IActionResult Delete(int id)
        {
            try
            {
                using var con = _db.GetConn();
                con.Open();
                using var cmd = new MySqlCommand("DELETE FROM Clientes WHERE Id=@id", con);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();

                TempData["Mensaje"] = "Cliente eliminado correctamente";
            }
            catch (MySqlException ex)
            {
                if (ex.Number == 1451) // error de FK (cliente con préstamos)
                {
                    TempData["Error"] = "❌ No se puede eliminar el cliente porque tiene préstamos registrados.";
                }
                else
                {
                    TempData["Error"] = "❌ Error al eliminar el cliente: " + ex.Message;
                }
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Muestra el detalle de un cliente por Id.
        /// </summary>
        public IActionResult Details(int id)
        {
            using var con = _db.GetConn();
            con.Open();
            using var cmd = new MySqlCommand("SELECT * FROM Clientes WHERE Id=@id", con);
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return NotFound();

            var c = new Cliente
            {
                Id = r.GetInt32("Id"),
                NombreCompleto = r.GetString("NombreCompleto"),
                Cedula = r.GetString("Cedula"),
                Telefono = r.GetString("Telefono"),
                Direccion = r.GetString("Direccion"),
                Estado = r.GetBoolean("Estado")
            };
            return View(c);
        }
    }
}
