using System.Globalization;
using Microsoft.Data.Sqlite;

namespace RestMock.Repositories;

public class SqliteDatabase
{
    private readonly string _connectionString;

    public SqliteDatabase(IWebHostEnvironment env)
        : this(Path.Combine(env.ContentRootPath, "mocks.db")) { }

    internal SqliteDatabase(string databasePath)
    {
        // Pooling disabled so the file handle is released after each operation (lets tests delete the file on Windows).
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
            ForeignKeys = true,
        }.ToString();

        EnsureSchema();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureSchema()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
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

            CREATE TABLE IF NOT EXISTS Mocks (
                Id             TEXT    NOT NULL PRIMARY KEY,
                ApplicationId  TEXT    NOT NULL REFERENCES Applications(Id) ON DELETE CASCADE,
                Position       INTEGER NOT NULL,
                HttpMethod     TEXT    NOT NULL,
                Pattern        TEXT    NOT NULL,
                StatusCode     INTEGER NOT NULL,
                ProcessingTime INTEGER NOT NULL,
                ContentType    TEXT    NOT NULL,
                Description    TEXT    NULL,
                ResponseBody   TEXT    NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Mocks_ApplicationId ON Mocks(ApplicationId, Position);
            """;
        command.ExecuteNonQuery();
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
