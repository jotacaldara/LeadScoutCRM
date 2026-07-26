namespace LeadScoutCRM.Models.Entities;

// Enum garante que os status são sempre valores válidos
public enum LeadStatus
{
    Novo = 0,
    MensagemEnviada = 1,
    EmNegociacao = 2,
    ClienteFechado = 3,
    Rejeitado = 4
}