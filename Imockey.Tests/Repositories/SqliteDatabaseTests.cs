using FluentAssertions;
using Microsoft.Data.Sqlite;
using Imockey.Domain;
using Imockey.Repositories;

namespace Imockey.Tests.Repositories;

public class SqliteDatabaseTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Constructor_CreatesDatabaseFile()
    {
        File.Exists(_db.Path).Should().BeTrue();
    }

    [Fact]
    public void Constructor_ExistingDatabase_KeepsData()
    {
        var user = _db.AddUser();

        var reopened = new UserRepository(new SqliteDatabase(_db.Path));

        reopened.GetById(user.Id).Should().NotBeNull();
    }

    [Fact]
    public void Constructor_EnablesWalJournalMode()
    {
        using var connection = _db.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode";

        command.ExecuteScalar().Should().Be("wal");
    }

    [Fact]
    public void Constructor_LegacyMocksTableWithoutEnabled_IsRenamedToRoutesWithColumnEnabled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"imockey_legacy_{Guid.NewGuid():N}.db");
        try
        {
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE Mocks (
                        Id TEXT NOT NULL PRIMARY KEY, ApplicationId TEXT NOT NULL, Position INTEGER NOT NULL,
                        HttpMethod TEXT NOT NULL, Pattern TEXT NOT NULL, StatusCode INTEGER NOT NULL,
                        ProcessingTime INTEGER NOT NULL, ContentType TEXT NOT NULL, Description TEXT NULL, ResponseBody TEXT NULL);
                    CREATE INDEX IX_Mocks_ApplicationId ON Mocks(ApplicationId, Position);
                    INSERT INTO Mocks VALUES ('3fa85f64-5717-4562-b3fc-2c963f66afa6', '00000000-0000-0000-0000-000000000001',
                        0, 'GET', '/legacy', 200, 0, 'application/json', NULL, NULL);
                    """;
                command.ExecuteNonQuery();
            }

            var database = new SqliteDatabase(path);

            new RouteRepository(database).GetById(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"))!.Enabled.Should().BeTrue();
            SchemaObjects(database).Should().Contain(["Routes", "IX_Routes_ApplicationId"])
                .And.NotContain(["Mocks", "IX_Mocks_ApplicationId"]);
        }
        finally
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                File.Delete(file);
        }
    }

    [Fact]
    public void Constructor_ExistingRoutesTable_IsKept()
    {
        var route = new RouteModel { ApplicationId = _db.AddApplication(_db.AddUser().Id, "kept").Id, Pattern = "/x" };
        _db.Routes.Add(route);

        var reopened = new SqliteDatabase(_db.Path);

        new RouteRepository(reopened).GetById(route.Id).Should().NotBeNull();
        SchemaObjects(reopened).Should().NotContain("Mocks");
    }

    private static List<string> SchemaObjects(SqliteDatabase database)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    [Fact]
    public void Constructor_MissingDirectory_CreatesIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"imockey_test_{Guid.NewGuid():N}");
        try
        {
            _ = new SqliteDatabase(Path.Combine(directory, "nested", "mocks.db"));

            File.Exists(Path.Combine(directory, "nested", "mocks.db")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ResolvePath_NotConfigured_UsesMocksDbInDataFolderOfContentRoot()
    {
        var contentRoot = Path.GetTempPath();
        var expected = Path.Combine(contentRoot, "data", "mocks.db");

        SqliteDatabase.ResolvePath(contentRoot, null).Should().Be(expected);
        SqliteDatabase.ResolvePath(contentRoot, " ").Should().Be(expected);
    }

    [Fact]
    public void ResolvePath_Relative_IsResolvedAgainstContentRoot()
    {
        var contentRoot = Path.GetTempPath();

        SqliteDatabase.ResolvePath(contentRoot, Path.Combine("data", "app.db"))
            .Should().Be(Path.Combine(contentRoot, "data", "app.db"));
    }

    [Fact]
    public void ResolvePath_Absolute_IsKept()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "elsewhere", "app.db");

        SqliteDatabase.ResolvePath(Path.Combine(Path.GetTempPath(), "root"), absolute).Should().Be(absolute);
    }

    [Fact]
    public void DeletingUser_CascadesToApplicationsAndRoutes()
    {
        var user = _db.AddUser();
        var application = _db.AddApplication(user.Id, "cascade");
        var route = new RouteModel { ApplicationId = application.Id, Pattern = "/x" };
        _db.Routes.Add(route);

        using (var connection = _db.Database.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM Users WHERE Id = $id";
            command.Parameters.AddWithValue("$id", user.Id.ToString());
            command.ExecuteNonQuery();
        }

        _db.Applications.GetById(application.Id).Should().BeNull();
        _db.Routes.GetById(route.Id).Should().BeNull();
    }
}
