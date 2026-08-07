using LeadScoutCRM.Models.Entities;
using LeadScoutCRM.Models.ViewModels;
using LeadScoutCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LeadScoutCRM.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly SubscriptionService _subscriptionService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        SubscriptionService subscriptionService,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Register(string? plan)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        // a validação a sério acontece depois do registo. (pre seleecionar planos)
        ViewBag.SelectedPlan = (plan == "Pro" || plan == "Business") ? plan : null;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        // Mantém o plano seleccionado visível na UI se o formulário voltar por erro
        ViewBag.SelectedPlan = (model.Plan == "Pro" || model.Plan == "Business") ? model.Plan : null;

        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName,
            Plan = SubscriptionPlan.Free,
            SubscriptionStatus = SubscriptionStatus.None,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true // desactivar em produção com email verification
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        // Atribui role "User" por omissão
        await _userManager.AddToRoleAsync(user, "User");

        // Faz login automático após registo
        await _signInManager.SignInAsync(user, isPersistent: false);

        _logger.LogInformation("Novo utilizador registado: {Email}", user.Email);

        // Se veio da landing page com um plano pago seleccionado, segue
        // directo para o Stripe Checkout em vez do Dashboard.
        if (model.Plan == "Pro" || model.Plan == "Business")
            return RedirectToAction(nameof(UpgradeRedirect), new { plan = model.Plan });

        return RedirectToAction("Index", "Home");
    }

  

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("Login: {Email}", model.Email);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "Conta bloqueada por demasiadas tentativas. Tenta em 5 minutos.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Email ou password incorrectos.");
        return View(model);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

 

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }


    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        var plan = PlanConfig.Plans[user.Plan];
        var leadCount = await _subscriptionService.GetLeadCountAsync(user.Id);

        ViewBag.User = user;
        ViewBag.Plan = plan;
        ViewBag.LeadCount = leadCount;

        return View();
    }


    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upgrade(string plan)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        if (!Enum.TryParse<SubscriptionPlan>(plan, out var targetPlan) ||
            targetPlan == SubscriptionPlan.Free)
        {
            TempData["Error"] = "Plano inválido.";
            return RedirectToAction("Settings");
        }

        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var checkoutUrl = await _subscriptionService
                .CreateCheckoutSessionAsync(user.Id, targetPlan, baseUrl);

            return Redirect(checkoutUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar sessão Stripe para {Email}", user.Email);
            TempData["Error"] = "Erro ao iniciar pagamento. Tenta novamente.";
            return RedirectToAction("Settings");
        }
    }

    //direto da landing vem pro checkout

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> UpgradeRedirect(string plan)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        if (!Enum.TryParse<SubscriptionPlan>(plan, out var targetPlan) ||
            targetPlan == SubscriptionPlan.Free)
        {
            return RedirectToAction("Index", "Home");
        }

        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var checkoutUrl = await _subscriptionService
                .CreateCheckoutSessionAsync(user.Id, targetPlan, baseUrl);

            return Redirect(checkoutUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar sessão Stripe (pós-registo) para {Email}", user.Email);
            TempData["Success"] = "Conta criada com sucesso! 🎉";
            TempData["Error"] = "Não foi possível iniciar o pagamento automaticamente — podes fazer upgrade aqui quando quiseres.";
            return RedirectToAction("Settings");
        }
    }

    // ── Portal de gestão Stripe (cancelar, actualizar cartão, etc.) ───────────

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageSubscription()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var portalUrl = await _subscriptionService
                .CreateCustomerPortalAsync(user.Id, baseUrl);

            return Redirect(portalUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao abrir portal Stripe para {Email}", user.Email);
            TempData["Error"] = "Erro ao abrir portal de subscrição.";
            return RedirectToAction("Settings");
        }
    }

    // ── Geração de chave API (plano Business) ──────────────────────────────────

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateApiKey()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        // Verificação no servidor — nunca confiar só na UI esconder o botão
        if (!PlanConfig.Plans[user.Plan].HasApiAccess)
        {
            TempData["Error"] = "O acesso à API está disponível apenas no plano Business.";
            return RedirectToAction("Settings");
        }

        var plainTextKey = ApiKeyHasher.GenerateKey();
        user.ApiKeyHash = ApiKeyHasher.Hash(plainTextKey);
        user.ApiKeyCreatedAt = DateTime.UtcNow;
        user.ApiKeyLastUsedAt = null;

        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Nova chave API gerada para {Email}", user.Email);

        // Mostrada apenas uma vez — a partir daqui só o hash existe na BD
        TempData["NewApiKey"] = plainTextKey;
        TempData["Success"] = "Chave API gerada com sucesso. Copia-a agora — não voltará a ser mostrada.";
        return RedirectToAction("Settings");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeApiKey()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        user.ApiKeyHash = null;
        user.ApiKeyCreatedAt = null;
        user.ApiKeyLastUsedAt = null;

        await _userManager.UpdateAsync(user);

        _logger.LogWarning("Chave API revogada para {Email}", user.Email);

        TempData["Success"] = "Chave API revogada. Qualquer integração que a use deixa de funcionar.";
        return RedirectToAction("Settings");
    }

    // ── Callback após Checkout bem sucedido ───────────────────────────────────

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> SubscriptionSuccess(string? session_id)
    {
        // O plano é activado pelo webhook — esta página é só feedback
        TempData["Success"] = "Subscrição activada! Bem-vindo ao plano premium. 🎉";
        return RedirectToAction("Settings");
    }
}