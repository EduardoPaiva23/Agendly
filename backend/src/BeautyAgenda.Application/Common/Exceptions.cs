using FluentValidation;
using FluentValidation.Results;

namespace BeautyAgenda.Application.Common;

/// <summary>Mapped to HTTP 404. The key is kept for logs/debugging but not echoed to the client.</summary>
public class NotFoundException(string resource, object key) : Exception(MessageFor(resource))
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;

    private static string MessageFor(string resource) => resource switch
    {
        "Appointment" => "Agendamento não encontrado.",
        "Customer" => "Cliente não encontrado.",
        "Service" => "Serviço não encontrado.",
        "Professional" => "Profissional não encontrado.",
        "Time-off" => "Bloqueio não encontrado.",
        _ => "Registro não encontrado.",
    };
}

/// <summary>Mapped to HTTP 409 (schedule conflict or incompatible state).</summary>
public class ConflictException(string message) : Exception(message);

public static class Validation
{
    public static ValidationException Error(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}
