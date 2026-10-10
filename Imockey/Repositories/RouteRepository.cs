using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Imockey.Domain;

namespace Imockey.Repositories;

public class RouteRepository
{
    private const string SelectColumns = """
        SELECT Id, ApplicationId, HttpMethod, Pattern, StatusCode, ProcessingTime, ContentType, Description, ResponseBody, Enabled
        FROM Routes
        """;

    private readonly SqliteDatabase _database;

    public RouteRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public IReadOnlyList<RouteModel> GetByApplication(Guid applicationId) =>
        Query($"{SelectColumns} WHERE ApplicationId = $value ORDER BY Position", applicationId.ToString());

    public RouteModel? GetById(Guid id) =>
        Query($"{SelectColumns} WHERE Id = $value", id.ToString()).FirstOrDefault();

    public int CountByApplication(Guid applicationId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Routes WHERE ApplicationId = $applicationId";
        command.Parameters.AddValue("$applicationId", applicationId.ToString());
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void Add(RouteModel route)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Routes (Id, ApplicationId, Position, HttpMethod, Pattern, StatusCode, ProcessingTime, ContentType, Description, ResponseBody, Enabled)
            VALUES (
                $id, $applicationId,
                (SELECT COALESCE(MAX(Position), -1) + 1 FROM Routes WHERE ApplicationId = $applicationId),
                $method, $pattern, $status, $time, $contentType, $description, $body, $enabled)
            """;
        AddFieldParameters(command, route);
        command.Parameters.AddValue("$applicationId", route.ApplicationId.ToString());
        command.Parameters.AddValue("$enabled", route.Enabled);
        command.ExecuteNonQuery();
    }

    // ApplicationId, position and enabled state are kept; a route never moves between applications
    // and toggling it goes through SetEnabled, so saving the edit form does not re-enable it.
    public bool Update(RouteModel route)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Routes SET
                HttpMethod = $method, Pattern = $pattern, StatusCode = $status, ProcessingTime = $time,
                ContentType = $contentType, Description = $description, ResponseBody = $body
            WHERE Id = $id
            """;
        AddFieldParameters(command, route);
        return command.ExecuteNonQuery() > 0;
    }

    public bool SetEnabled(Guid id, bool enabled)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Routes SET Enabled = $enabled WHERE Id = $id";
        command.Parameters.AddValue("$id", id.ToString());
        command.Parameters.AddValue("$enabled", enabled);
        return command.ExecuteNonQuery() > 0;
    }

    public bool Delete(Guid id)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Routes WHERE Id = $id";
        command.Parameters.AddValue("$id", id.ToString());
        return command.ExecuteNonQuery() > 0;
    }

    private static void AddFieldParameters(SqliteCommand command, RouteModel route)
    {
        command.Parameters.AddValue("$id", route.Id.ToString());
        command.Parameters.AddValue("$method", route.HttpMethod);
        command.Parameters.AddValue("$pattern", route.Pattern);
        command.Parameters.AddValue("$status", route.StatusCode);
        command.Parameters.AddValue("$time", route.ProcessingTime);
        command.Parameters.AddValue("$contentType", route.ContentType);
        command.Parameters.AddValue("$description", route.Description);
        // Serialized as JSON so a string body comes back as string and a nested object as JObject/JArray.
        command.Parameters.AddValue("$body", route.ResponseBody is null ? null : JsonConvert.SerializeObject(route.ResponseBody));
    }

    private List<RouteModel> Query(string sql, string value)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddValue("$value", value);

        var result = new List<RouteModel>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(Read(reader));
        return result;
    }

    private static RouteModel Read(SqliteDataReader reader) => new()
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
        Enabled = reader.GetBoolean(9),
    };
}
