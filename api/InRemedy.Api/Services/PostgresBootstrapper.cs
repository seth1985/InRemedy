using Npgsql;

namespace InRemedy.Api.Services;

public static class PostgresBootstrapper
{
    public static async Task EnsureDatabaseExistsAsync(string connectionString, CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("The PostgreSQL connection string must include a database name.");
        }

        var maintenanceBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres"
        };

        await using var connection = new NpgsqlConnection(maintenanceBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var existsCommand = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @database", connection);
        existsCommand.Parameters.AddWithValue("database", databaseName);
        var exists = await existsCommand.ExecuteScalarAsync(cancellationToken) is not null;

        if (exists)
        {
            return;
        }

        await using var createCommand = new NpgsqlCommand($@"CREATE DATABASE ""{databaseName}""", connection);
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task EnsureMigrationHistoryBaselineAsync(
        string connectionString,
        string migrationId,
        string productVersion,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string historyExistsSql = """
SELECT EXISTS (
    SELECT 1
    FROM information_schema.tables
    WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory'
)
""";

        await using var historyExistsCommand = new NpgsqlCommand(historyExistsSql, connection);
        var historyExists = (bool)(await historyExistsCommand.ExecuteScalarAsync(cancellationToken) ?? false);

        const string appTablesExistSql = """
SELECT EXISTS (
    SELECT 1
    FROM information_schema.tables
    WHERE table_schema = 'public' AND table_name IN ('Devices', 'Remediations', 'RemediationResults', 'SavedViews')
)
""";

        await using var appTablesExistCommand = new NpgsqlCommand(appTablesExistSql, connection);
        var appTablesExist = (bool)(await appTablesExistCommand.ExecuteScalarAsync(cancellationToken) ?? false);

        if (!appTablesExist)
        {
            return;
        }

        if (!historyExists)
        {
            const string createHistorySql = """
CREATE TABLE "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
)
""";
            await using var createHistoryCommand = new NpgsqlCommand(createHistorySql, connection);
            await createHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string rowExistsSql = """SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = @migrationId)""";
        await using var rowExistsCommand = new NpgsqlCommand(rowExistsSql, connection);
        rowExistsCommand.Parameters.AddWithValue("migrationId", migrationId);
        var rowExists = (bool)(await rowExistsCommand.ExecuteScalarAsync(cancellationToken) ?? false);

        if (rowExists)
        {
            return;
        }

        const string insertHistorySql = """INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES (@migrationId, @productVersion)""";
        await using var insertHistoryCommand = new NpgsqlCommand(insertHistorySql, connection);
        insertHistoryCommand.Parameters.AddWithValue("migrationId", migrationId);
        insertHistoryCommand.Parameters.AddWithValue("productVersion", productVersion);
        await insertHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task ResetApplicationDataAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string truncateSql = """
TRUNCATE TABLE
  "ImportErrors",
  "ImportStagingRows",
  "RemediationResults",
  "Devices",
  "Remediations",
  "SavedViews",
  "ImportBatches"
RESTART IDENTITY CASCADE
""";

        await using var truncateCommand = new NpgsqlCommand(truncateSql, connection);
        await truncateCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
