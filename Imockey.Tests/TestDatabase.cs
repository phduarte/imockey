using Imockey.Domain;
using Imockey.Repositories;

namespace Imockey.Tests;

// Fresh SQLite file per test class instance; deleted on Dispose.
public sealed class TestDatabase : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"imockey_test_{Guid.NewGuid():N}.db");

    public SqliteDatabase Database { get; }
    public UserRepository Users { get; }
    public ApplicationRepository Applications { get; }
    public RouteRepository Routes { get; }

    public TestDatabase()
    {
        Database = new SqliteDatabase(Path);
        Users = new UserRepository(Database);
        Applications = new ApplicationRepository(Database);
        Routes = new RouteRepository(Database);
    }

    public UserModel AddUser(string userName = "alice")
    {
        var user = new UserModel { UserName = $"{userName}-{Guid.NewGuid():N}"[..20], DisplayName = userName };
        Users.Add(user);
        return user;
    }

    public ApplicationModel AddApplication(Guid userId, string slug)
    {
        var application = new ApplicationModel { UserId = userId, Name = slug, Slug = slug };
        Applications.Add(application);
        return application;
    }

    public void Dispose()
    {
        foreach (var file in new[] { Path, Path + "-wal", Path + "-shm" })
            if (File.Exists(file))
                File.Delete(file);
    }
}
