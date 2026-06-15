namespace ECommerceOrderProcessing.Shared.Errors;

/// <summary>
/// Canonical error code strings returned in <c>ErrorResponse.Code</c>.
/// Clients use these values to decide whether to retry, surface a user message, or escalate.
/// </summary>
public static class ErrorCodes
{
    // General
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string BusinessRuleViolation = "BUSINESS_RULE_VIOLATION";
    public const string InternalError = "INTERNAL_ERROR";
    public const string Conflict = "CONFLICT";

    // Order
    public const string InvalidOrder = "INVALID_ORDER";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string OrderAlreadyConfirmed = "ORDER_ALREADY_CONFIRMED";
    public const string OrderAlreadyCancelled = "ORDER_ALREADY_CANCELLED";

    // Payment
    public const string PaymentGatewayTimeout = "PAYMENT_GATEWAY_TIMEOUT";
    public const string PaymentGatewayUnavailable = "PAYMENT_GATEWAY_UNAVAILABLE";
    public const string PaymentDeclined = "PAYMENT_DECLINED";
    public const string PaymentNotFound = "PAYMENT_NOT_FOUND";
    public const string RefundFailed = "REFUND_FAILED";
    public const string DuplicatePayment = "DUPLICATE_PAYMENT";

    // Inventory
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
    public const string ProductNotFound = "PRODUCT_NOT_FOUND";
    public const string ReservationNotFound = "RESERVATION_NOT_FOUND";
    public const string ReservationExpired = "RESERVATION_EXPIRED";

    // Shipping
    public const string InvalidShippingAddress = "INVALID_SHIPPING_ADDRESS";
    public const string CarrierUnavailable = "CARRIER_UNAVAILABLE";
    public const string ShipmentNotFound = "SHIPMENT_NOT_FOUND";
    public const string ShipmentAlreadyDispatched = "SHIPMENT_ALREADY_DISPATCHED";

    // Notification
    public const string NotificationProviderUnavailable = "NOTIFICATION_PROVIDER_UNAVAILABLE";
    public const string InvalidNotificationTarget = "INVALID_NOTIFICATION_TARGET";
    public const string NotificationNotFound = "NOTIFICATION_NOT_FOUND";

    // Rate limiting
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";

    // Idempotency
    public const string IdempotencyKeyConflict = "IDEMPOTENCY_KEY_CONFLICT";
    public const string InvalidIdempotencyKey = "INVALID_IDEMPOTENCY_KEY";

    // Auth
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string TokenExpired = "TOKEN_EXPIRED";
}

/// <summary>
/// Error codes that clients should retry (with exponential back-off).
/// Codes not in this set indicate a permanent failure that retrying will not resolve.
/// </summary>
public static class RetryableErrorCodes
{
    public static readonly IReadOnlySet<string> Values = new HashSet<string>
    {
        ErrorCodes.PaymentGatewayTimeout,
        ErrorCodes.PaymentGatewayUnavailable,
        ErrorCodes.CarrierUnavailable,
        ErrorCodes.NotificationProviderUnavailable,
        ErrorCodes.InternalError,
        ErrorCodes.RateLimitExceeded
    };

    public static bool IsRetryable(string errorCode) => Values.Contains(errorCode);
}
