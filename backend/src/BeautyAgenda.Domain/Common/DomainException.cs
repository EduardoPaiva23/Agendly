namespace BeautyAgenda.Domain.Common;

/// <summary>Raised when an operation would violate a business invariant (mapped to HTTP 409).</summary>
public class DomainException(string message) : Exception(message);
