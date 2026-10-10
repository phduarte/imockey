using FluentAssertions;
using RestMock.Domain;
using RestMock.Repositories;

namespace RestMock.Tests.Repositories;

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
    public void DeletingUser_CascadesToApplicationsAndMocks()
    {
        var user = _db.AddUser();
        var application = _db.AddApplication(user.Id, "cascade");
        var mock = new EndpointModel { ApplicationId = application.Id, Pattern = "/x" };
        _db.Mocks.Add(mock);

        using (var connection = _db.Database.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM Users WHERE Id = $id";
            command.Parameters.AddWithValue("$id", user.Id.ToString());
            command.ExecuteNonQuery();
        }

        _db.Applications.GetById(application.Id).Should().BeNull();
        _db.Mocks.GetById(mock.Id).Should().BeNull();
    }
}
