using LeadScoutCRM.Models.Entities;

namespace LeadScoutCRM.Services;

public static class LeadScoringService
{
    public static int CalculateScore(Lead lead)
    {
        var score = 0;

        // Dados básicos (40 pontos)
        if (!string.IsNullOrEmpty(lead.PhoneNumber)) score += 20;
        if (!string.IsNullOrEmpty(lead.Website)) score += 15;
        if (!string.IsNullOrEmpty(lead.Address)) score += 5;

        // Engajamento (40 pontos)
        score += lead.Status switch
        {
            LeadStatus.MensagemEnviada => 15,
            LeadStatus.EmNegociacao => 30,
            LeadStatus.ClienteFechado => 40,
            LeadStatus.Rejeitado => 0,
            _ => 0
        };

        // Notas e actividade (20 pontos)
        var notesCount = lead.Notes?.Count ?? 0;
        score += Math.Min(notesCount * 5, 15);
        if (lead.LastContactedAt.HasValue) score += 5;

        return Math.Min(score, 100);
    }

    public static (string Label, string Css) GetScoreBadge(int score) => score switch
    {
        >= 75 => ("Quente 🔥", "background:#dcfce7;color:#15803d"),
        >= 45 => ("Morno", "background:#fef9c3;color:#a16207"),
        >= 20 => ("Frio", "background:#dbeafe;color:#1d4ed8"),
        _ => ("Novo", "background:#f1f5f9;color:#64748b")
    };
}