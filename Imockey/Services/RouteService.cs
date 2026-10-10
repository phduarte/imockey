using Imockey.Domain;
using Imockey.Repositories;

namespace Imockey.Services;

public class RouteService
{
    private readonly RouteRepository _repository;

    // Argument is the id of the application whose routes changed.
    public event Action<Guid>? OnChange;

    public RouteService(RouteRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<RouteModel> GetByApplication(Guid applicationId) => _repository.GetByApplication(applicationId);

    public void Add(Guid applicationId, RouteModel route)
    {
        route.ApplicationId = applicationId;
        _repository.Add(route);
        OnChange?.Invoke(applicationId);
    }

    public void Update(Guid applicationId, Guid id, RouteModel updated)
    {
        EnsureBelongsTo(applicationId, id);
        updated.Id = id;
        updated.ApplicationId = applicationId;
        _repository.Update(updated);
        OnChange?.Invoke(applicationId);
    }

    public void SetEnabled(Guid applicationId, Guid id, bool enabled)
    {
        EnsureBelongsTo(applicationId, id);
        _repository.SetEnabled(id, enabled);
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
            throw new ArgumentException("Rota não encontrada nesta aplicação.", nameof(id));
    }
}
