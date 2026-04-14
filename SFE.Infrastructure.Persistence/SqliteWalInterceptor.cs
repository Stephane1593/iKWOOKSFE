// File: SFE.Infrastructure/Persistence/SqliteWalInterceptor.cs
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace SFE.Infrastructure.Persistence;

/// <summary>
/// Executes WAL + busy_timeout PRAGMAs on every new SQLite connection.
/// Prevents "database is locked" errors by:
///  - WAL: allows concurrent reads while writing
///  - busy_timeout: retries instead of failing immediately on write contention
///  - synchronous=NORMAL: safe with WAL, better performance
/// </summary>
public class SqliteWalInterceptor : DbConnectionInterceptor
{
    private const string Pragmas = """
        PRAGMA journal_mode = WAL;
        PRAGMA busy_timeout = 5000;
        PRAGMA synchronous = NORMAL;
    """;

    public override void ConnectionOpened(
        DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = Pragmas;
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Pragmas;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}