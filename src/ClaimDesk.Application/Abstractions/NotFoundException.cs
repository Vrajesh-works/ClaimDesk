namespace ClaimDesk.Application.Abstractions;

public sealed class NotFoundException(string message) : Exception(message);
