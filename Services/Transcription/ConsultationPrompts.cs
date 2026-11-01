using System.Text.Json;

namespace MultiClinica.API.Services.Transcription;

public sealed record SoapReport(
    string? Subjective, string? Objective, string? Assessment, string? Plan, int? SuggestedReturnDays);

public static class ConsultationPrompts
{
    public const string SoapSystem = """
        Você organiza a transcrição de uma consulta em um relatório SOAP em português.

        Regras:
        - Use somente o que foi dito na consulta; não invente fatos, sintomas, exames, diagnósticos ou condutas.
        - Não registre diagnóstico que o profissional não tenha dito.
        - Quando uma seção não tiver informação, escreva exatamente "Não mencionado.".
        - subjective: o que o paciente relatou.
        - objective: achados e observações do profissional.
        - assessment: avaliação do profissional.
        - plan: conduta e orientações combinadas.
        - suggestedReturnDays: número de dias até o retorno somente se um retorno tiver sido combinado; caso contrário, null.

        Responda somente com um objeto JSON, sem texto antes ou depois, neste formato:
        {"subjective":"...","objective":"...","assessment":"...","plan":"...","suggestedReturnDays":null}
        """;

    // Aceita JSON puro ou dentro de bloco ```json ... ```.
    public static SoapReport? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var json = content.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = json.IndexOf('\n');
            var closingFence = json.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLineEnd < 0 || closingFence <= firstLineEnd) return null;
            json = json[(firstLineEnd + 1)..closingFence].Trim();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            string? Text(string name) =>
                root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
                    ? element.GetString()
                    : null;

            int? Number(string name) =>
                root.TryGetProperty(name, out var element)
                    && element.ValueKind == JsonValueKind.Number
                    && element.TryGetInt32(out var value)
                        ? value
                        : null;

            return new SoapReport(
                Text("subjective"), Text("objective"), Text("assessment"), Text("plan"),
                Number("suggestedReturnDays"));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
