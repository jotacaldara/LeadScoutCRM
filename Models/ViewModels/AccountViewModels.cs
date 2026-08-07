using System.ComponentModel.DataAnnotations;

namespace LeadScoutCRM.Models.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "O nome é obrigatório")]
    [Display(Name = "Nome de apresentação")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O email é obrigatório")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "A password é obrigatória")]
    [MinLength(8, ErrorMessage = "Mínimo 8 caracteres")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma a password")]
    [Compare("Password", ErrorMessage = "As passwords não coincidem")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    // Plano escolhido na landing page (ex: "Pro", "Business").
    // Opcional — null/inválido significa plano Free (comportamento por omissão).
    public string? Plan { get; set; }
}

public class LoginViewModel
{
    [Required(ErrorMessage = "O email é obrigatório")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "A password é obrigatória")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Lembrar-me")]
    public bool RememberMe { get; set; }
}