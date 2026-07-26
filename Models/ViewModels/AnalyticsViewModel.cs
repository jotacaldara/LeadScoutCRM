namespace LeadScoutCRM.Models.ViewModels;

public class AnalyticsViewModel
{
    // Para os gráficos
    public Dictionary<string, int> LeadsByStatus { get; set; } = new();
    public Dictionary<string, int> LeadsByNiche { get; set; } = new();
    public Dictionary<string, int> LeadsPerDay { get; set; } = new(); // últimos 30 dias

    // KPIs
    public int TotalLeads { get; set; }
    public int LeadsThisWeek { get; set; }
    public double ConversionRate { get; set; } // % ClienteFechado / Total
    public double ContactRate { get; set; }    // % com LastContactedAt
    public string TopNiche { get; set; } = "—";
    public string BestConversionNiche { get; set; } = "—";
}