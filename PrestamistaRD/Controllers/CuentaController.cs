using Microsoft.AspNetCore.Mvc;
using PrestamistaRD.Data;
using PrestamistaRD.Models;
using MySql.Data.MySqlClient;
using BCrypt.Net;

namespace PrestamistaRD.Controllers
{
    /// <summary>
    /// Controlador encargado de la autenticación de usuarios:
    /// manejo de login, validación de credenciales y cierre de sesión.
    /// </summary>
    public class CuentaController : Controller
    {
        private readonly Db _db;
        public CuentaController(Db db) => _db = db;

        /// <summary>
        /// Muestra la vista de inicio de sesión.
        /// </summary>
        public IActionResult Login() => View();

        /// <summary>
        /// Procesa el inicio de sesión del usuario.
        /// Valida credenciales contra la base de datos y crea la sesión.
        /// </summary>
        [HttpPost]
        public IActionResult Login(string correo, string clave)
        {
            using var con = _db.GetConn();
            con.Open();
            using var cmd = new MySqlCommand("SELECT * FROM Usuarios WHERE Correo=@c AND Estado=1", con);
            cmd.Parameters.AddWithValue("@c", correo);
            using var r = cmd.ExecuteReader();

            if (r.Read())
            {
                string hash = r.GetString("ClaveHash");
                if (BCrypt.Net.BCrypt.Verify(clave, hash))
                {
                    // 🔹 Guardar datos en sesión
                    HttpContext.Session.SetInt32("UsuarioId", r.GetInt32("Id"));
                    HttpContext.Session.SetString("Nombre", r.GetString("Nombre"));
                    HttpContext.Session.SetString("Rol", r.GetString("Rol"));

                    return RedirectToAction("Index", "Home");
                }
            }

            // 🔹 Mensaje de error si las credenciales no son válidas
            ViewBag.Error = "❌ Usuario o contraseña incorrectos.";
            return View();
        }

        /// <summary>
        /// Cierra la sesión del usuario y redirige al login.
        /// </summary>
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
