using System.Xml.Linq;
using FluentAssertions;
using Imockey.Repositories;

namespace Imockey.Tests.Repositories;

public class DataProtectionKeyRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly DataProtectionKeyRepository _keys;

    public DataProtectionKeyRepositoryTests()
    {
        _keys = new DataProtectionKeyRepository(_db.Database);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void GetAllElements_Empty_ReturnsEmpty()
    {
        _keys.GetAllElements().Should().BeEmpty();
    }

    [Fact]
    public void StoreElement_RoundTripsXml()
    {
        var key = new XElement("key", new XAttribute("id", "1"), new XElement("value", "secret"));

        _keys.StoreElement(key, "key-1");

        _keys.GetAllElements().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(key);
    }

    [Fact]
    public void StoreElement_SameFriendlyName_ReplacesXml()
    {
        _keys.StoreElement(new XElement("key", "old"), "key-1");
        _keys.StoreElement(new XElement("key", "new"), "key-1");

        _keys.GetAllElements().Should().ContainSingle().Which.Value.Should().Be("new");
    }

    [Fact]
    public void StoreElement_SurvivesReopeningDatabase()
    {
        _keys.StoreElement(new XElement("key", "persisted"), "key-1");

        var reopened = new DataProtectionKeyRepository(new SqliteDatabase(_db.Path));

        reopened.GetAllElements().Should().ContainSingle().Which.Value.Should().Be("persisted");
    }
}
