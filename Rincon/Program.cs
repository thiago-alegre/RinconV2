using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rincon.DataAccess.Data;
using Rincon.Models;
using Rincon.Utilities;
using System.Globalization;
using Rincon.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.FileProviders;

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

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys");
builder.Services.Configure<ForwardedHeadersOptions>(ProductionSafety.ConfigureProxy);
builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
if (builder.Environment.IsProduction() &&
    (string.IsNullOrWhiteSpace(builder.Configuration["AllowedHosts"]) ||
     builder.Configuration["AllowedHosts"]!.Split(';').Any(host => host.Trim() == "*")))
    throw new InvalidOperationException("Configure explicit AllowedHosts before production startup.");
var dataProtectionBuilder = Microsoft.AspNetCore.DataProtection.DataProtectionBuilderExtensions
    .PersistKeysToFileSystem(builder.Services.AddDataProtection(), new DirectoryInfo(dataProtectionKeysPath));
Microsoft.AspNetCore.DataProtection.DataProtectionBuilderExtensions
    .SetApplicationName(dataProtectionBuilder, "RinconV2");

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 12;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();
// Revalidate permissions/stamps on every authenticated request while the system is small.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

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
builder.Services.AddRazorPages();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.Events.OnValidatePrincipal = async context =>
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal?.Identity?.IsAuthenticated != true) return;
        var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.GetUserAsync(context.Principal);
        if (user is null || !user.IsActive)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
    options.Events.OnRedirectToLogin = context =>
    {
        // An expired POST must never be replayed or become a GET to a POST-only action.
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            return ProductionSafety.WriteError(context.HttpContext, 401);
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

var app = builder.Build();
ProductionSafety.CheckKeys(app.Services, dataProtectionKeysPath);
var configuredProductImagesPath = builder.Configuration["Storage:ProductImagesPath"];
var productImagesPath = string.IsNullOrWhiteSpace(configuredProductImagesPath)
    ? Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "imagenes", "products")
    : Path.GetFullPath(configuredProductImagesPath);
Directory.CreateDirectory(productImagesPath);
app.UseForwardedHeaders();
app.UseRequestEvidence();

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
    app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        AllowStatusCode404Response = true,
        ExceptionHandler = context => ProductionSafety.WriteError(context,
            ProductionSafety.IsDatabaseConflict(context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()!.Error) ? 409 : 500)
    });
    app.UseHsts();
}

app.UseStatusCodePages(context => ProductionSafety.WriteError(context.HttpContext, context.HttpContext.Response.StatusCode));
app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(productImagesPath),
    RequestPath = "/imagenes/products"
});
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area=Employee}/{controller=Balance}/{action=Index}/{id?}");

app.MapRazorPages();
app.MapGet("/health/live", () => Results.StatusCode(200)).AllowAnonymous();
app.MapGet("/health/ready", async (ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(3));
    try
    {
        return await db.Database.CanConnectAsync(timeout.Token)
            ? Results.StatusCode(200) : Results.StatusCode(503);
    }
    catch { return Results.StatusCode(503); }
}).AllowAnonymous();

app.Run();
