using FluentValidation;
using FluentValidation.Results;

namespace BeautyAgenda.Application.Common;

/// <summary>Mapped to HTTP 404.</summary>
public class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.");

/// <summary>Mapped to HTTP 409 (schedule conflict or incompatible state).</summary>
public class ConflictException(string message) : Exception(message);

public static class Validation
{
    public static ValidationException Error(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}
