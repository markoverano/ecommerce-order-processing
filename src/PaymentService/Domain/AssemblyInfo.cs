using System.Runtime.CompilerServices;

// Infrastructure needs PaymentInitiated for event-store projection; no other assembly should reference it.
[assembly: InternalsVisibleTo("PaymentService.Infrastructure")]
[assembly: InternalsVisibleTo("PaymentService.Domain.Tests")]
