using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SasPortal.Persistence.Common;

/// <summary>
/// Detects whether an exception originates from a database connectivity / availability problem
/// (e.g. server unreachable, authentication rejected at the PostgreSQL level, admin shutdown)
/// rather than from a regular validation or business rule violation.
/// </summary>
public static class DatabaseExceptionClassifier
{
    private static readonly HashSet<string> ConnectivitySqlStates = new(StringComparer.Ordinal)
    {
        // Class 08 — Connection Exception
        "08000",
        "08001",
        "08003",
        "08004",
        "08006",
        "08007",

        // Class 28 — Invalid Authorization Specification (covers pg_hba.conf rejections)
        "28000",
        "28P01",

        // 53300 — too_many_connections
        "53300",

        // Class 57 — Operator Intervention (admin shutdown / cannot connect now)
        "57P01",
        "57P02",
        "57P03",
    };

    public static bool IsDatabaseConnectivityException(Exception? exception)
    {
        if (exception is null)
        {
            return false;
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case PostgresException postgresException:
                    if (IsConnectivitySqlState(postgresException.SqlState))
                    {
                        return true;
                    }

                    // PostgresException with a non-connectivity SqlState (e.g. 23505 unique violation)
                    // is a business/data error and must not be misclassified as connectivity.
                    return false;

                case NpgsqlException:
                case SocketException:
                case TimeoutException:
                    return true;

                case DbUpdateException:
                    // Walk the inner exception chain to inspect the underlying provider error.
                    continue;
            }
        }

        return false;
    }

    private static bool IsConnectivitySqlState(string? sqlState)
    {
        return !string.IsNullOrEmpty(sqlState) && ConnectivitySqlStates.Contains(sqlState);
    }
}
