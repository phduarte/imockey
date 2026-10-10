using RestMock.Domain;
using RestMock.Repositories;

namespace RestMock.Services;

public class MockClientService
{
    private readonly MockRepository _repository;

    // Argument is the id of the application whose mocks changed.
    public event Action<Guid>? OnChange;

    public MockClientService(MockRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<EndpointModel> GetByApplication(Guid applicationId) => _repository.GetByApplication(applicationId);

    public void Add(Guid applicationId, EndpointModel endpoint)
    {
        endpoint.ApplicationId = applicationId;
        _repository.Add(endpoint);
        OnChange?.Invoke(applicationId);
    }

    public void Update(Guid applicationId, Guid id, EndpointModel updated)
    {
        EnsureBelongsTo(applicationId, id);
        updated.Id = id;
        updated.ApplicationId = applicationId;
        _repository.Update(updated);
        OnChange?.Invoke(applicationId);
    }

    public void Remove(Guid applicationId, Guid id)
    {
        EnsureBelongsTo(applicationId, id);
        _repository.Delete(id);
        OnChange?.Invoke(applicationId);
    }

    private void EnsureBelongsTo(Guid applicationId, Guid id)
    {
        if (_repository.GetById(id)?.ApplicationId != applicationId)
            throw new ArgumentException("Mock não encontrado nesta aplicação.", nameof(id));
    }
}
