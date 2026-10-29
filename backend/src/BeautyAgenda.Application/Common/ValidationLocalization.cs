using System.Globalization;
using FluentValidation;

namespace BeautyAgenda.Application.Common;

/// <summary>
/// Built-in FluentValidation messages in pt-BR, with Portuguese field names in the text
/// (error keys stay as the original property names, so clients can map them to form fields).
/// </summary>
public static class ValidationLocalization
{
    private static readonly Dictionary<string, string> DisplayNames = new()
    {
        ["Name"] = "Nome",
        ["CustomerName"] = "Nome",
        ["Phone"] = "Telefone",
        ["CustomerPhone"] = "Celular",
        ["Email"] = "E-mail",
        ["CustomerEmail"] = "E-mail",
        ["Password"] = "Senha",
        ["Description"] = "Descrição",
        ["DurationMinutes"] = "Duração",
        ["Price"] = "Preço",
        ["ServiceIds"] = "Serviços",
        ["ProfessionalId"] = "Profissional",
        ["StartAt"] = "Início",
        ["EndAt"] = "Fim",
        ["StartTime"] = "Início",
        ["EndTime"] = "Fim",
        ["DayOfWeek"] = "Dia da semana",
        ["Date"] = "Data",
        ["Reason"] = "Motivo",
        ["Status"] = "Status",
        ["Items"] = "Intervalos",
    };

    public static void Configure()
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("pt-BR");
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) =>
            member is not null && DisplayNames.TryGetValue(member.Name, out var name) ? name : null;
    }
}
