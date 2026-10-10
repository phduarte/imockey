using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Imockey.Repositories;

public class SqliteDatabase
{
    private readonly string _connectionString;

    public SqliteDatabase(IWebHostEnvironment env, IConfiguration configuration)
        : this(ResolvePath(env.ContentRootPath, configuration["Database:Path"])) { }

    internal SqliteDatabase(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);

        // Pooling disabled so the file handle is released after each operation (lets tests delete the file on Windows).
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
            ForeignKeys = true,
        }.ToString();

        EnsureSchema();
    }

    // Relative paths are resolved against the content root; unset means "data/mocks.db" there.
    internal static string ResolvePath(string contentRootPath, string? configuredPath) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath) ? Path.Combine("data", "mocks.db") : configuredPath, contentRootPath);

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureSchema()
    {
        using var connection = Open();
        RenameLegacyMocksTable(connection);

        using var command = connection.CreateCommand();
        // WAL is persistent in the file and required by Litestream, which replicates the database in production.
        command.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS Users (
                Id           TEXT NOT NULL PRIMARY KEY,
                DisplayName  TEXT NOT NULL,
                UserName     TEXT NULL UNIQUE COLLATE NOCASE,
                PasswordHash TEXT NULL,
                GitHubId     TEXT NULL UNIQUE,
                CreatedAt    TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Applications (
                Id          TEXT NOT NULL PRIMARY KEY,
                UserId      TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
                Name        TEXT NOT NULL,
                Slug        TEXT NOT NULL UNIQUE COLLATE NOCASE,
                Description TEXT NULL,
                CreatedAt   TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Applications_UserId ON Applications(UserId);

            CREATE TABLE IF NOT EXISTS Routes (
                Id             TEXT    NOT NULL PRIMARY KEY,
                ApplicationId  TEXT    NOT NULL REFERENCES Applications(Id) ON DELETE CASCADE,
                Position       INTEGER NOT NULL,
                HttpMethod     TEXT    NOT NULL,
                Pattern        TEXT    NOT NULL,
                StatusCode     INTEGER NOT NULL,
                ProcessingTime INTEGER NOT NULL,
                ContentType    TEXT    NOT NULL,
                Description    TEXT    NULL,
                ResponseBody   TEXT    NULL,
                Enabled        INTEGER NOT NULL DEFAULT 1
            );

            CREATE INDEX IF NOT EXISTS IX_Routes_ApplicationId ON Routes(ApplicationId, Position);

            CREATE TABLE IF NOT EXISTS DataProtectionKeys (
                FriendlyName TEXT NOT NULL PRIMARY KEY,
                Xml          TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();

        AddColumnIfMissing(connection, "Routes", "Enabled", "INTEGER NOT NULL DEFAULT 1");
    }

    // Databases created before the rename keep their routes in a "Mocks" table; its old index is dropped
    // so IX_Routes_ApplicationId is created in its place.
    private static void RenameLegacyMocksTable(SqliteConnection connection)
    {
        using var check = connection.CreateCommand();
        check.CommandText = """
            SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Mocks')
               AND NOT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Routes')
            """;
        if (Convert.ToInt32(check.ExecuteScalar()) == 0)
            return;

        using var rename = connection.CreateCommand();
        rename.CommandText = """
            ALTER TABLE Mocks RENAME TO Routes;
            DROP INDEX IF EXISTS IX_Mocks_ApplicationId;
            """;
        rename.ExecuteNonQuery();
    }

    // CREATE TABLE IF NOT EXISTS leaves existing tables untouched, so columns added later need an ALTER TABLE.
    private static void AddColumnIfMissing(SqliteConnection connection, string table, string column, string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $column";
        check.Parameters.AddValue("$column", column);
        if (Convert.ToInt32(check.ExecuteScalar()) > 0)
            return;

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        alter.ExecuteNonQuery();
    }
}

internal static class SqliteExtensions
{
    public static void AddValue(this SqliteParameterCollection parameters, string name, object? value) =>
        parameters.AddWithValue(name, value ?? DBNull.Value);

    public static string ToDbValue(this DateTime value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static DateTime GetUtcDateTime(this SqliteDataReader reader, int ordinal) =>
        DateTime.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static string? GetNullableString(this SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
