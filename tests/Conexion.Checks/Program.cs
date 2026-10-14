using Cazador.Api.Modules.Obtencion.Ia;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Net.Sockets;

// Diagnóstico de desarrollo: SELECT 1, sin migraciones ni cambios de datos.
var config = new ConfigurationBuilder().AddUserSecrets(typeof(GeminiOfferReader).Assembly).AddEnvironmentVariables().Build();
var value = config.GetConnectionString("MySql");
if (string.IsNullOrWhiteSpace(value))
{
    Console.WriteLine("Falta ConnectionStrings:MySql en la configuración local.");
    Environment.ExitCode = 1; return;
}
try
{
    var connectionString = new MySqlConnectionStringBuilder(value) { Pooling = false, ConnectionTimeout = 15 };
    using var connection = new MySqlConnection(connectionString.ConnectionString);
    await connection.OpenAsync();
    using var command = new MySqlCommand("SELECT 1", connection) { CommandTimeout = 15 };
    if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1) throw new InvalidDataException("Resultado de comprobación inesperado");
    Console.WriteLine("OK: conexión nueva con MySQL y SELECT 1 correctos. No se modificaron datos.");
}
catch (MySqlException ex)
{
    Console.WriteLine($"MySQL: código {ex.Number}; SQLSTATE {ex.SqlState ?? "sin dato"}; causa {ex.GetBaseException().GetType().Name}.");
    for (Exception? cause = ex; cause is not null; cause = cause.InnerException)
        if (cause is SocketException socket) Console.WriteLine($"Red: {socket.SocketErrorCode}.");
    Console.WriteLine("No se muestran contraseñas ni cadenas de conexión.");
    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    Console.WriteLine($"Comprobación no completada: {ex.GetType().Name}. No se muestran datos de acceso.");
    Environment.ExitCode = 1;
}
