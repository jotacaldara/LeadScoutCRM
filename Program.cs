using LeadScoutCRM.Data;
using LeadScoutCRM.Models.Entities;
using LeadScoutCRM.Services;
using LeadScoutCRM.Services.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// ── Stripe global config ──
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllersWithViews();

// ── Base de dados ──
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── ASP.NET Core Identity ──
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Configuração de password (ajusta ao teu gosto)
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;

    // Email único obrigatório
    options.User.RequireUniqueEmail = true;

    // Desliga confirmação de email por enquanto (activar em produção)
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ── Cookie de autenticação ──
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// ── Serviços de negócio ──
builder.Services.Configure<GooglePlacesOptions>(
    builder.Configuration.GetSection(GooglePlacesOptions.SectionName));
builder.Services.AddHttpClient<IGooglePlacesService, GooglePlacesService>();
builder.Services.AddScoped<LeadScoutCRM.Services.SubscriptionService>();

builder.Services.AddHttpClient<IAiService, GeminiService>();
builder.Services.AddTransient<IEmailService, EmailService>();

var app = builder.Build();

// ── Seed: criar roles e admin inicial ──
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();