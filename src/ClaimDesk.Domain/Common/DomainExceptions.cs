namespace ClaimDesk.Domain.Common;

/// <summary>Base type for business rule violations raised by the domain.</summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>The requested action is not valid for the current state (HTTP 409).</summary>
public sealed class InvalidTransitionException(string message) : DomainException(message);

/// <summary>The actor is not allowed to perform the action on this claim (HTTP 403).</summary>
public sealed class ActionNotPermittedException(string message) : DomainException(message);

/// <summary>Input failed a business validation rule (HTTP 400).</summary>
public sealed class DomainValidationException(string message) : DomainException(message);
