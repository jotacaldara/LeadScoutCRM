using LeadScoutCRM.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LeadScoutCRM.Controllers;

public class PricingController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public PricingController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Plans = PlanConfig.Plans;
        ViewBag.CurrentPlan = SubscriptionPlan.Free;

        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.CurrentPlan = user?.Plan ?? SubscriptionPlan.Free;
        }

        return View();
    }
}