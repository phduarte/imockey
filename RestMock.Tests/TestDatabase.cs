using RestMock.Domain;
using RestMock.Repositories;

namespace RestMock.Tests;

// Fresh SQLite file per test class instance; deleted on Dispose.
public sealed class TestDatabase : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"restmock_test_{Guid.NewGuid():N}.db");

    public SqliteDatabase Database { get; }
    public UserRepository Users { get; }
    public ApplicationRepository Applications { get; }
    public MockRepository Mocks { get; }

    public TestDatabase()
    {
        Database = new SqliteDatabase(Path);
        Users = new UserRepository(Database);
        Applications = new ApplicationRepository(Database);
        Mocks = new MockRepository(Database);
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
        if (File.Exists(Path))
            File.Delete(Path);
    }
}
