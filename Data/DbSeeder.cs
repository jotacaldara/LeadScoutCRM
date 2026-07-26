using LeadScoutCRM.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace LeadScoutCRM.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILogger<AppDbContext>>();

        // ── Criar roles se não existirem ──
        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Criar Admin inicial ──
        var adminEmail = config["AdminAccount:Email"] ?? "admin@leadscout.com";
        var adminPassword = config["AdminAccount:Password"] ?? "Admin@12345!";

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                DisplayName = "Administrador",
                Plan = SubscriptionPlan.Business,
                SubscriptionStatus = SubscriptionStatus.Active,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            // ── BYPASS password validators: hash directly ──────────────────────────
            // Permite usar qualquer password no appsettings, independente das regras
            // de Identity (mínimo 8 chars, etc). Apenas para o admin seed.
            var hasher = new PasswordHasher<ApplicationUser>();
            admin.PasswordHash = hasher.HashPassword(admin, adminPassword);

            // CreateAsync sem password = não executa os PasswordValidators
            var result = await userManager.CreateAsync(admin);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                await userManager.AddToRoleAsync(admin, "User");
                logger.LogInformation("Admin criado: {Email}", adminEmail);
            }
            else
            {
                logger.LogError("Erro ao criar admin: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            // Garante que o utilizador existente tem a role Admin e o plano correcto
            if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
            {
                await userManager.AddToRoleAsync(existingAdmin, "Admin");
                logger.LogInformation("Role Admin atribuída ao utilizador existente: {Email}", adminEmail);
            }

            // Actualiza a password do admin existente (permite mudar via appsettings)
            var token = await userManager.GeneratePasswordResetTokenAsync(existingAdmin);
            var resetResult = await userManager.ResetPasswordAsync(existingAdmin, token, adminPassword);

            if (!resetResult.Succeeded)
            {
                // Se falhar (password fraca), usa hash directo como fallback
                var hasher = new PasswordHasher<ApplicationUser>();
                existingAdmin.PasswordHash = hasher.HashPassword(existingAdmin, adminPassword);
                await userManager.UpdateAsync(existingAdmin);
                logger.LogWarning(
                    "Password do admin actualizada via hash directo (a password configurada não cumpre as regras de Identity).");
            }
        }
    }
}