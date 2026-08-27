using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rincon.DataAccess.Data;
using Rincon.Models;
using Rincon.Utilities;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("ConexionPostgres");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'ConexionPostgres' is not configured. Set it with user-secrets locally or with the environment variable 'ConnectionStrings__ConexionPostgres' in production.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys");
var dataProtectionBuilder = Microsoft.AspNetCore.DataProtection.DataProtectionBuilderExtensions
    .PersistKeysToFileSystem(builder.Services.AddDataProtection(), new DirectoryInfo(dataProtectionKeysPath));
Microsoft.AspNetCore.DataProtection.DataProtectionBuilderExtensions
    .SetApplicationName(dataProtectionBuilder, "RinconV2");

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

builder.Services.AddControllersWithViews(options =>
{
    var messages = options.ModelBindingMessageProvider;
    messages.SetAttemptedValueIsInvalidAccessor((value, fieldName) =>
        $"El valor '{value}' no es válido.");
    messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value =>
        $"El valor '{value}' no es válido.");
    messages.SetValueIsInvalidAccessor(value =>
        $"El valor '{value}' no es válido.");
    messages.SetValueMustBeANumberAccessor(fieldName =>
        "Ingrese un número válido.");
});

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

var app = builder.Build();

var spanishCulture = CultureInfo.GetCultureInfo("es-AR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(spanishCulture),
    SupportedCultures = new[] { spanishCulture },
    SupportedUICultures = new[] { spanishCulture }
});

// Crear roles automáticamente si no existen
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles =
    {
        SD.Role_Admin,
        SD.Role_Employee,
        SD.Role_God
    };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var adminEmail = builder.Configuration["BootstrapAdmin:Email"];
    var adminPassword = builder.Configuration["BootstrapAdmin:Password"];

    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Administrador RinconV2",
                DNI = "ADMIN",
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException("No se pudo crear el administrador inicial: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await userManager.CheckPasswordAsync(admin, adminPassword))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(admin);
            var resetResult = await userManager.ResetPasswordAsync(admin, resetToken, adminPassword);
            if (!resetResult.Succeeded)
                throw new InvalidOperationException("No se pudo restablecer la contraseña del administrador: " + string.Join("; ", resetResult.Errors.Select(e => e.Description)));
        }

        if (!await userManager.IsInRoleAsync(admin, SD.Role_Admin))
            await userManager.AddToRoleAsync(admin, SD.Role_Admin);
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area=Employee}/{controller=Balance}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
