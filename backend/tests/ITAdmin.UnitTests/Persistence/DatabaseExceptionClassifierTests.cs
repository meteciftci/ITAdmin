using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ITAdmin.Persistence.Common;

namespace ITAdmin.UnitTests.Persistence;

public sealed class DatabaseExceptionClassifierTests
{
    [Fact]
    public void IsDatabaseConnectivityException_WhenExceptionIsNull_ReturnsFalse()
    {
        Assert.False(DatabaseExceptionClassifier.IsDatabaseConnectivityException(null));
    }

    [Fact]
    public void IsDatabaseConnectivityException_WhenPlainInvalidOperationException_ReturnsFalse()
    {
        var exception = new InvalidOperationException("not a connectivity issue");

        Assert.False(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    [Fact]
    public void IsDatabaseConnectivityException_WhenArgumentException_ReturnsFalse()
    {
        var exception = new ArgumentException("validation");

        Assert.False(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    [Fact]
    public void IsDatabaseConnectivityException_WhenTimeoutException_ReturnsTrue()
    {
        var exception = new TimeoutException("connection timed out");

        Assert.True(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    [Fact]
    public void IsDatabaseConnectivityException_WhenSocketException_ReturnsTrue()
    {
        var exception = new SocketException();

        Assert.True(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    [Fact]
    public void IsDatabaseConnectivityException_WhenNpgsqlException_ReturnsTrue()
    {
        var exception = new NpgsqlException("npgsql failure");

        Assert.True(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    [Fact]
    public void IsDatabaseConnectivityException_WhenInvalidOperationWrapsNpgsqlException_ReturnsTrue()
    {
        var exception = new InvalidOperationException(
            "An error occurred while opening the connection.",
            new NpgsqlException("Failed to connect"));

        Assert.True(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    [Fact]
    public void IsDatabaseConnectivityException_WhenInvalidOperationWrapsSocketException_ReturnsTrue()
    {
        var exception = new InvalidOperationException(
            "Connection refused.",
            new SocketException());

        Assert.True(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    [Fact]
    public void IsDatabaseConnectivityException_WhenDbUpdateExceptionWrapsNpgsqlException_ReturnsTrue()
    {
        var exception = new DbUpdateException(
            "An error occurred while updating the entries.",
            new NpgsqlException("Connection lost"));

        Assert.True(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    [Fact]
    public void IsDatabaseConnectivityException_WhenDbUpdateExceptionWithoutInner_ReturnsFalse()
    {
        var exception = new DbUpdateException(
            "An error occurred while updating the entries.",
            (Exception?)null);

        Assert.False(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    [Theory]
    [InlineData("08000")]
    [InlineData("08001")]
    [InlineData("08003")]
    [InlineData("08004")]
    [InlineData("08006")]
    [InlineData("08007")]
    [InlineData("28000")]
    [InlineData("28P01")]
    [InlineData("53300")]
    [InlineData("57P01")]
    [InlineData("57P02")]
    [InlineData("57P03")]
    public void IsDatabaseConnectivityException_WhenPostgresExceptionWithConnectivitySqlState_ReturnsTrue(string sqlState)
    {
        var exception = CreatePostgresException(sqlState);

        Assert.True(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    [Theory]
    [InlineData("23505")] // unique_violation
    [InlineData("23503")] // foreign_key_violation
    [InlineData("22001")] // string_data_right_truncation
    [InlineData("42P01")] // undefined_table
    public void IsDatabaseConnectivityException_WhenPostgresExceptionWithBusinessSqlState_ReturnsFalse(string sqlState)
    {
        var exception = CreatePostgresException(sqlState);

        Assert.False(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    [Fact]
    public void IsDatabaseConnectivityException_WhenDbUpdateExceptionWrapsConnectivityPostgresException_ReturnsTrue()
    {
        var exception = new DbUpdateException(
            "Failed to save.",
            CreatePostgresException("28000"));

        Assert.True(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    [Fact]
    public void IsDatabaseConnectivityException_WhenDbUpdateExceptionWrapsBusinessPostgresException_ReturnsFalse()
    {
        var exception = new DbUpdateException(
            "Failed to save.",
            CreatePostgresException("23505"));

        Assert.False(DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception));
    }

    private static PostgresException CreatePostgresException(string sqlState)
    {
        return new PostgresException(
            messageText: $"simulated error for sql state {sqlState}",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState);
    }
}
