using LeadScoutCRM.Models.Entities;

namespace LeadScoutCRM.Models.ViewModels;


// Agrupa todas as leads por status para renderizar o board Kanban.

public class KanbanViewModel
{
    // Chave = status (coluna), Valor = leads nessa coluna ordenadas por data desc
    public Dictionary<LeadStatus, List<Lead>> Columns { get; set; } = new();

    public int TotalLeads { get; set; }

    // Helpers para o header de cada coluna
    public int CountFor(LeadStatus status) =>
        Columns.TryGetValue(status, out var list) ? list.Count : 0;
}