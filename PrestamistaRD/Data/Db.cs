using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;

namespace PrestamistaRD.Data
{
    public class Db
    {
        private readonly string _cs;
        public Db(IConfiguration cfg) => _cs = cfg.GetConnectionString("DefaultConnection")!;
        public MySqlConnection GetConn() => new MySqlConnection(_cs);
    }
}
