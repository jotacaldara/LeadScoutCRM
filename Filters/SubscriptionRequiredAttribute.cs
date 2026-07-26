using LeadScoutCRM.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LeadScoutCRM.Filters;

// Uso: [SubscriptionRequired(SubscriptionPlan.Pro)]
public class SubscriptionRequiredAttribute : TypeFilterAttribute
{
    public SubscriptionRequiredAttribute(SubscriptionPlan minimumPlan)
        : base(typeof(SubscriptionRequiredFilter))
    {
        Arguments = new object[] { minimumPlan };
    }
}

public class SubscriptionRequiredFilter : IAsyncActionFilter
{
    private readonly SubscriptionPlan _minimumPlan;
    private readonly UserManager<ApplicationUser> _userManager;

    public SubscriptionRequiredFilter(
        SubscriptionPlan minimumPlan,
        UserManager<ApplicationUser> userManager)
    {
        _minimumPlan = minimumPlan;
        _userManager = userManager;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = await _userManager.GetUserAsync(context.HttpContext.User);

        if (user == null || user.Plan < _minimumPlan)
        {
            // Redireciona para a página de pricing com mensagem
            context.HttpContext.Response.Headers["X-Upgrade-Required"] = "true";
            context.Result = new RedirectToActionResult("Index", "Pricing", new
            {
                upgradeRequired = true,
                requiredPlan = _minimumPlan.ToString()
            });
            return;
        }

        await next();
    }
}