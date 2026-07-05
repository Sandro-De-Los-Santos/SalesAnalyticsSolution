using Microsoft.Data.SqlClient;

namespace ETL.Core.Data
{
    public class DatabaseConnection
    {
        private const string ConnectionString =
            "Server=Sandro;Database=AnalyticDB;Trusted_Connection=True;TrustServerCertificate=True;";

        public SqlConnection GetConnection()
        {
            var connection = new SqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }
    }
}