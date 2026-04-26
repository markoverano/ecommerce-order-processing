namespace ECommerceOrderProcessing.Shared.Domain;

public abstract class AggregateRoot
{
    private readonly List<DomainEvent> _uncommittedEvents = new();

    public Guid Id { get; protected set; }
    public int Version { get; protected set; }

    public IReadOnlyList<DomainEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    protected void RaiseEvent(DomainEvent domainEvent)
    {
        _uncommittedEvents.Add(domainEvent);
        Apply(domainEvent);
        Version++;
    }

    // Each aggregate implements Apply to mutate state from its own events.
    protected abstract void Apply(DomainEvent domainEvent);

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();
}
