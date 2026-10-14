using System.Data.Common;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;

namespace Cazador.Api.Infrastructure.Persistence;

public sealed record ConnectionFailure(string Tipo, string Causa, int? CodigoMySql,
    string? SqlState, string? Red, bool CancelacionSolicitada, bool EsCancelacion);

public sealed class ConnectionDiagnostics(ILogger<ConnectionDiagnostics> logger) : DbConnectionInterceptor
{
    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
        => Registrar(eventData.Exception, CancellationToken.None);

    public override Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Registrar(eventData.Exception, cancellationToken);
        return Task.CompletedTask;
    }

    public static ConnectionFailure Describir(Exception exception, CancellationToken ct = default)
    {
        MySqlException? mysql = null;
        SocketException? socket = null;
        var cancellation = false;
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            mysql ??= current as MySqlException;
            socket ??= current as SocketException;
            cancellation |= current is OperationCanceledException;
        }
        // Solo códigos y nombres de tipos; nunca Message, connection string, host,
        // contraseña, cuerpo de petición ni el contenido de una excepción completa.
        return new ConnectionFailure(exception.GetType().Name, exception.GetBaseException().GetType().Name,
            mysql?.Number, mysql?.SqlState, socket?.SocketErrorCode.ToString(),
            ct.IsCancellationRequested, cancellation && ct.IsCancellationRequested);
    }

    private void Registrar(Exception ex, CancellationToken ct)
    {
        var d = Describir(ex, ct);
        var level = d.EsCancelacion ? LogLevel.Information : LogLevel.Error;
        logger.Log(level,
            "Diagnóstico MySQL: tipo={Tipo}; causa={Causa}; código={Codigo}; SQLSTATE={SqlState}; red={Red}; cancelación solicitada={Cancelacion}; operación cancelada={EsCancelacion}.",
            d.Tipo, d.Causa, d.CodigoMySql, d.SqlState, d.Red, d.CancelacionSolicitada, d.EsCancelacion);
    }
}
