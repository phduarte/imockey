using Microsoft.Data.Sqlite;
using Imockey.Domain;

namespace Imockey.Repositories;

public class ApplicationRepository
{
    private const string SelectColumns = "SELECT Id, UserId, Name, Slug, Description, CreatedAt FROM Applications";

    private readonly SqliteDatabase _database;

    public ApplicationRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public IReadOnlyList<ApplicationModel> GetByUser(Guid userId) =>
        Query($"{SelectColumns} WHERE UserId = $value ORDER BY CreatedAt, Name", userId.ToString());

    public ApplicationModel? GetById(Guid id) =>
        Query($"{SelectColumns} WHERE Id = $value", id.ToString()).FirstOrDefault();

    public ApplicationModel? GetBySlug(string slug) =>
        Query($"{SelectColumns} WHERE Slug = $value", slug).FirstOrDefault();

    public void Add(ApplicationModel application)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Applications (Id, UserId, Name, Slug, Description, CreatedAt)
            VALUES ($id, $userId, $name, $slug, $description, $createdAt)
            """;
        command.Parameters.AddValue("$id", application.Id.ToString());
        command.Parameters.AddValue("$userId", application.UserId.ToString());
        command.Parameters.AddValue("$name", application.Name);
        command.Parameters.AddValue("$slug", application.Slug);
        command.Parameters.AddValue("$description", application.Description);
        command.Parameters.AddValue("$createdAt", application.CreatedAt.ToDbValue());
        command.ExecuteNonQuery();
    }

    public bool Delete(Guid id)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Applications WHERE Id = $id";
        command.Parameters.AddValue("$id", id.ToString());
        return command.ExecuteNonQuery() > 0;
    }

    private List<ApplicationModel> Query(string sql, string value)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddValue("$value", value);

        var result = new List<ApplicationModel>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(Read(reader));
        return result;
    }

    private static ApplicationModel Read(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        UserId = Guid.Parse(reader.GetString(1)),
        Name = reader.GetString(2),
        Slug = reader.GetString(3),
        Description = reader.GetNullableString(4),
        CreatedAt = reader.GetUtcDateTime(5),
    };
}
