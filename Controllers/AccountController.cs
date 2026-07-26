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
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
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
        return RedirectToAction("Index", "Home");
    }

    // ── Login ─────────────────────────────────────────────────────────────────

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

    // ── Logout ────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    // ── Página de acesso negado ───────────────────────────────────────────────

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // ── Definições da conta ───────────────────────────────────────────────────

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

    // ── Upgrade de plano (redireciona para Stripe Checkout) ───────────────────

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