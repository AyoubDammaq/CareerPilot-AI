namespace CareerPilot.Application.Common;

public sealed class ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class ConcurrencyConflictException(string message) : Exception(message);
