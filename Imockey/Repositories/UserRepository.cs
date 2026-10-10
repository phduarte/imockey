using Microsoft.Data.Sqlite;
using Imockey.Domain;

namespace Imockey.Repositories;

public class UserRepository
{
    private const string SelectColumns = "SELECT Id, DisplayName, UserName, PasswordHash, GitHubId, CreatedAt FROM Users";

    private readonly SqliteDatabase _database;

    public UserRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public UserModel? GetById(Guid id) =>
        QuerySingle($"{SelectColumns} WHERE Id = $value", id.ToString());

    public UserModel? GetByUserName(string userName) =>
        QuerySingle($"{SelectColumns} WHERE UserName = $value", userName);

    public UserModel? GetByGitHubId(string gitHubId) =>
        QuerySingle($"{SelectColumns} WHERE GitHubId = $value", gitHubId);

    public void Add(UserModel user)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (Id, DisplayName, UserName, PasswordHash, GitHubId, CreatedAt)
            VALUES ($id, $displayName, $userName, $passwordHash, $gitHubId, $createdAt)
            """;
        command.Parameters.AddValue("$id", user.Id.ToString());
        command.Parameters.AddValue("$displayName", user.DisplayName);
        command.Parameters.AddValue("$userName", user.UserName);
        command.Parameters.AddValue("$passwordHash", user.PasswordHash);
        command.Parameters.AddValue("$gitHubId", user.GitHubId);
        command.Parameters.AddValue("$createdAt", user.CreatedAt.ToDbValue());
        command.ExecuteNonQuery();
    }

    public void SetUserName(Guid id, string userName)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET UserName = $userName WHERE Id = $id";
        command.Parameters.AddValue("$id", id.ToString());
        command.Parameters.AddValue("$userName", userName);
        command.ExecuteNonQuery();
    }

    private UserModel? QuerySingle(string sql, string value)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddValue("$value", value);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    private static UserModel Read(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        DisplayName = reader.GetString(1),
        UserName = reader.GetNullableString(2),
        PasswordHash = reader.GetNullableString(3),
        GitHubId = reader.GetNullableString(4),
        CreatedAt = reader.GetUtcDateTime(5),
    };
}
