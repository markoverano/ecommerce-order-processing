using System.Runtime.CompilerServices;

// Infrastructure needs domain events for event-store projection and outbox mapping.
[assembly: InternalsVisibleTo("InventoryService.Infrastructure")]
[assembly: InternalsVisibleTo("InventoryService.Domain.Tests")]
