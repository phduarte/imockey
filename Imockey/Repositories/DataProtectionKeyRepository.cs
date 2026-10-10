using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace Imockey.Repositories;

// The default key folder lives inside the container and is lost on every scale-to-zero restart, which would invalidate
// every auth cookie. Keeping the key ring in mocks.db lets it travel with the database (and its Litestream replica).
public class DataProtectionKeyRepository : IXmlRepository
{
    private readonly SqliteDatabase _database;

    public DataProtectionKeyRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Xml FROM DataProtectionKeys";

        using var reader = command.ExecuteReader();
        var elements = new List<XElement>();
        while (reader.Read())
            elements.Add(XElement.Parse(reader.GetString(0)));
        return elements;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO DataProtectionKeys (FriendlyName, Xml) VALUES ($friendlyName, $xml)
            ON CONFLICT (FriendlyName) DO UPDATE SET Xml = excluded.Xml
            """;
        command.Parameters.AddValue("$friendlyName", friendlyName);
        command.Parameters.AddValue("$xml", element.ToString(SaveOptions.DisableFormatting));
        command.ExecuteNonQuery();
    }
}
