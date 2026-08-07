using LeadScoutCRM.Auth;
using LeadScoutCRM.Data;
using LeadScoutCRM.Models.Entities;
using LeadScoutCRM.Services;
using LeadScoutCRM.Services.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// ── Stripe global config ──
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Chave API do plano Business. Formato: lsk_XXXXXXXXXXXXXXXXXXXX"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("ApiKey", document)] = new List<string>()
    });
});
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

// ── Autenticação por API Key (plano Business) ──
// Esquema adicional ao cookie do Identity — não interfere com o login normal.
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, options => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiAccess", policy =>
        policy.RequireClaim("has_api_access", "true"));
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

// Swagger fica acessível sempre (não só em Development) para permitir
// demonstrar a API pública. Numa app 100% produção real, isto seria
app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
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