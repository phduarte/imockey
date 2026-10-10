using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using RestMock.Domain;

namespace RestMock.Repositories;

public class MockRepository
{
    private const string SelectColumns = """
        SELECT Id, ApplicationId, HttpMethod, Pattern, StatusCode, ProcessingTime, ContentType, Description, ResponseBody
        FROM Mocks
        """;

    private readonly SqliteDatabase _database;

    public MockRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public IReadOnlyList<EndpointModel> GetByApplication(Guid applicationId) =>
        Query($"{SelectColumns} WHERE ApplicationId = $value ORDER BY Position", applicationId.ToString());

    public EndpointModel? GetById(Guid id) =>
        Query($"{SelectColumns} WHERE Id = $value", id.ToString()).FirstOrDefault();

    public int CountByApplication(Guid applicationId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Mocks WHERE ApplicationId = $applicationId";
        command.Parameters.AddValue("$applicationId", applicationId.ToString());
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void Add(EndpointModel endpoint)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Mocks (Id, ApplicationId, Position, HttpMethod, Pattern, StatusCode, ProcessingTime, ContentType, Description, ResponseBody)
            VALUES (
                $id, $applicationId,
                (SELECT COALESCE(MAX(Position), -1) + 1 FROM Mocks WHERE ApplicationId = $applicationId),
                $method, $pattern, $status, $time, $contentType, $description, $body)
            """;
        AddFieldParameters(command, endpoint);
        command.Parameters.AddValue("$applicationId", endpoint.ApplicationId.ToString());
        command.ExecuteNonQuery();
    }

    // ApplicationId and position are kept; a mock never moves between applications.
    public bool Update(EndpointModel endpoint)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Mocks SET
                HttpMethod = $method, Pattern = $pattern, StatusCode = $status, ProcessingTime = $time,
                ContentType = $contentType, Description = $description, ResponseBody = $body
            WHERE Id = $id
            """;
        AddFieldParameters(command, endpoint);
        return command.ExecuteNonQuery() > 0;
    }

    public bool Delete(Guid id)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Mocks WHERE Id = $id";
        command.Parameters.AddValue("$id", id.ToString());
        return command.ExecuteNonQuery() > 0;
    }

    private static void AddFieldParameters(SqliteCommand command, EndpointModel endpoint)
    {
        command.Parameters.AddValue("$id", endpoint.Id.ToString());
        command.Parameters.AddValue("$method", endpoint.HttpMethod);
        command.Parameters.AddValue("$pattern", endpoint.Pattern);
        command.Parameters.AddValue("$status", endpoint.StatusCode);
        command.Parameters.AddValue("$time", endpoint.ProcessingTime);
        command.Parameters.AddValue("$contentType", endpoint.ContentType);
        command.Parameters.AddValue("$description", endpoint.Description);
        // Serialized as JSON so a string body comes back as string and a nested object as JObject/JArray.
        command.Parameters.AddValue("$body", endpoint.ResponseBody is null ? null : JsonConvert.SerializeObject(endpoint.ResponseBody));
    }

    private List<EndpointModel> Query(string sql, string value)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddValue("$value", value);

        var result = new List<EndpointModel>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(Read(reader));
        return result;
    }

    private static EndpointModel Read(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        ApplicationId = Guid.Parse(reader.GetString(1)),
        HttpMethod = reader.GetString(2),
        Pattern = reader.GetString(3),
        StatusCode = reader.GetInt32(4),
        ProcessingTime = reader.GetInt32(5),
        ContentType = reader.GetString(6),
        Description = reader.GetNullableString(7),
        ResponseBody = reader.IsDBNull(8) ? null : JsonConvert.DeserializeObject(reader.GetString(8)),
    };
}
