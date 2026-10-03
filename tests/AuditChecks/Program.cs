using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rincon.DataAccess.Data;
using Rincon.Infrastructure;
using Rincon.Models;

const string connection = "Host=127.0.0.1;Port=55439;Database=rinconv2_hardening;Username=audit";
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var keys = Path.Combine(root, "audit-keys");
Environment.SetEnvironmentVariable("ConnectionStrings__ConexionPostgres", connection);
Environment.SetEnvironmentVariable("DataProtection__KeysPath", keys);
Environment.SetEnvironmentVariable("AllowedHosts", "localhost");
var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
await using var db = new ApplicationDbContext(dbOptions);
await db.Database.MigrateAsync();
var product = new Product { Name = "Synthetic audit product", SalePrice = 100, PurchasePrice = 50, Quantity = 1000 };
var account = new PersonalAccount { FullName = "Synthetic audit account", DNI = Guid.NewGuid().ToString(), isActive = true };
db.AddRange(product, account);
await db.SaveChangesAsync();
var checks = 0;
void Check(bool result, string name)
{
    if (!result) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); checks++;
}
string Field(string html, string name)
{
    var tag = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>").Value;
    return WebUtility.HtmlDecode(Regex.Match(tag, "value=\"([^\"]*)\"").Groups[1].Value);
}
var factory = new AuditFactory(root, keys, connection);
string? loginCookie = null;
using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
async Task Login(HttpClient browser)
{
    var html = await browser.GetStringAsync("/Identity/Account/Login");
    var response = await browser.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string>
    {
        ["__RequestVerificationToken"] = Field(html,"__RequestVerificationToken"),
        ["Input.Email"] = "audit@example.invalid", ["Input.Password"] = "Synthetic-Audit-Only!42"
    }));
    Check(response.StatusCode == HttpStatusCode.Redirect, "login with fresh antiforgery token");
    loginCookie = string.Join("; ", response.Headers.GetValues("Set-Cookie")
        .Select(value=>value.Split(';')[0]).Where(value=>value.StartsWith(".AspNetCore.Identity.Application")));
}
Check((await client.PostAsync("/Employee/Sales/Create", new StringContent(""))).StatusCode == HttpStatusCode.Unauthorized, "expired/anonymous POST returns 401 without unsafe ReturnUrl");
await Login(client);
Check((await client.GetAsync("/Employee/Sales/Void")).StatusCode == HttpStatusCode.NotFound, "cancellation screen requires an existing sale");
var html = await client.GetStringAsync("/Employee/Sales/Create");
var before = await db.DirectSales.CountAsync();
var invalid = await client.PostAsync("/Employee/Sales/Create", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"]="invalid" }));
Check(invalid.StatusCode == HttpStatusCode.BadRequest && await db.DirectSales.CountAsync() == before, "invalid antiforgery rejects without sale");
Check((await invalid.Content.ReadAsStringAsync()).Contains("Referencia:"), "400 provides correlation and safe recovery");
async Task<Dictionary<string,string>> Form(int method)
{
    var page = await client.GetStringAsync("/Employee/Sales/Create");
    return new()
    {
        ["__RequestVerificationToken"] = Field(page,"__RequestVerificationToken"),
        ["OperationId"] = Field(page,"OperationId"), ["PaymentMethod"] = method.ToString(),
        ["Lines[0].ProductId"] = product.Id.ToString(), ["Lines[0].Quantity"]="1",
        ["CashAmount"]="40", ["TransferAmount"]="60", ["PersonalAccountId"]=account.Id.ToString()
    };
}
foreach (var method in new[]{1,2,3,4})
{
    var form = await Form(method);
    var response = await client.PostAsync("/Employee/Sales/Create", new FormUrlEncodedContent(form));
    Check(response.StatusCode == HttpStatusCode.Redirect, "sale payment method " + method);
    var retry = await client.PostAsync("/Employee/Sales/Create", new FormUrlEncodedContent(form));
    Check(retry.Headers.Location == response.Headers.Location, "lost-response retry recovers same sale " + method);
}
Check(await db.DirectSales.CountAsync() == before + 4, "four confirmed sales despite eight submissions");
await db.Entry(product).ReloadAsync();
Check(product.Quantity == 996, "stock decremented exactly once per sale");
var duplicateForm = await Form(1);
var concurrent = await Task.WhenAll(Enumerable.Range(0,2).Select(_ => client.PostAsync("/Employee/Sales/Create", new FormUrlEncodedContent(duplicateForm))));
Check(concurrent.All(r => r.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Conflict), "concurrent duplicate handled without 500");
var operation = Guid.Parse(duplicateForm["OperationId"]);
Check(await db.DirectSales.CountAsync(s => s.OperationId == operation) == 1, "unique operation prevents concurrent duplicate");
var stockBefore = await db.DirectSales.CountAsync();
await db.Entry(product).ReloadAsync(); product.Quantity=1; await db.SaveChangesAsync();
var stockForm1=await Form(1); var stockForm2=await Form(1);
await Task.WhenAll(client.PostAsync("/Employee/Sales/Create",new FormUrlEncodedContent(stockForm1)),client.PostAsync("/Employee/Sales/Create",new FormUrlEncodedContent(stockForm2)));
await db.Entry(product).ReloadAsync();
Check(product.Quantity==0 && await db.DirectSales.CountAsync()==stockBefore+1,"concurrent distinct sales cannot oversell last unit");
product.Quantity=100; await db.SaveChangesAsync();
var payHtml=await client.GetStringAsync("/Employee/Accounts/Detail/"+account.Id);
var payForm=new Dictionary<string,string>{["__RequestVerificationToken"]=Field(payHtml,"__RequestVerificationToken"),["OperationId"]=Field(payHtml,"OperationId"),["Id"]=account.Id.ToString(),["AmountText"]="100",["PaymentMethod"]="1"};
await Task.WhenAll(client.PostAsync("/Employee/Accounts/Pay",new FormUrlEncodedContent(payForm)),client.PostAsync("/Employee/Accounts/Pay",new FormUrlEncodedContent(payForm)));
Check(await db.PersonalAccountPayments.Where(p=>p.PersonalAccountId==account.Id).SumAsync(p=>p.Amount)==100,"concurrent full-debt payments cannot overcollect");
var failure=await client.GetAsync("/Employee/Sales/GetAll?dateTo=9999-12-31");
Check(failure.IsSuccessStatusCode,"maximum date no longer produces an overflow/500");
await CommercialChecks.Run(factory.Services, client, dbOptions, Field, Check);
var oldForm = await Form(1);
var logoutHtml = await client.GetStringAsync("/Employee/Sales/Create");
var logout = await client.PostAsync("/Identity/Account/Logout", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = Field(logoutHtml,"__RequestVerificationToken") }));
Check(logout.StatusCode == HttpStatusCode.Redirect, "logout POST with antiforgery");
Check((await client.PostAsync("/Employee/Sales/Create",new FormUrlEncodedContent(oldForm))).StatusCode == HttpStatusCode.Unauthorized, "old tab after logout cannot submit sale");
await Login(client);
var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
var anonymousToken = Field(await anonymous.GetStringAsync("/Identity/Account/Login"), "__RequestVerificationToken");
Check((await client.PostAsync("/Employee/Sales/Create", new FormUrlEncodedContent(new Dictionary<string,string>{["__RequestVerificationToken"]=anonymousToken}))).StatusCode == HttpStatusCode.BadRequest, "token from different identity/cookie rejected");
Check((await client.GetAsync("/health/live")).IsSuccessStatusCode && (await client.GetAsync("/health/ready")).IsSuccessStatusCode, "liveness and PostgreSQL readiness");

// Exercise the production proxy configuration with explicit remote addresses.
using var proxyServer = new TestServer(new WebHostBuilder().ConfigureServices(services => services.Configure<ForwardedHeadersOptions>(ProductionSafety.ConfigureProxy))
    .Configure(app => { app.UseForwardedHeaders(); app.Run(ctx => ctx.Response.WriteAsync(ctx.Request.Scheme)); }));
foreach (var trusted in new[]{true,false})
{
    var result = await proxyServer.SendAsync(ctx =>
    {
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(trusted ? "127.0.0.1" : "203.0.113.5");
        ctx.Request.Scheme="http"; ctx.Request.Headers["X-Forwarded-Proto"]="https";
        ctx.Request.Headers["X-Forwarded-For"]="198.51.100.2";
    });
    using var reader = new StreamReader(result.Response.Body);
    Check(await reader.ReadToEndAsync() == (trusted ? "https":"http"), "proxy trust=" + trusted);
}
var provider1 = DataProtectionProvider.Create(new DirectoryInfo(keys), options => options.SetApplicationName("RinconV2"));
var protectedValue = provider1.CreateProtector("audit").Protect("persisted");
var services = new ServiceCollection();
services.AddLogging(); services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys)).SetApplicationName("RinconV2");
using var keyServices = services.BuildServiceProvider();
keyServices.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddDays(90));
var provider2 = DataProtectionProvider.Create(new DirectoryInfo(keys), options => options.SetApplicationName("RinconV2"));
Check(provider2.CreateProtector("audit").Unprotect(protectedValue)=="persisted", "old payload readable after isolated key renewal and new provider");
var blockedPath=Path.Combine(root,"not-a-key-directory"); File.WriteAllText(blockedPath,"test");
try { ProductionSafety.CheckKeys(keyServices,blockedPath); Check(false,"invalid key storage must fail"); }
catch(IOException) { Check(true,"unusable key storage fails before serving"); }
File.Delete(blockedPath);
await factory.DisposeAsync();
await using var restarted = new AuditFactory(root, keys, connection);
using var existingBrowser = restarted.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress=new Uri("https://localhost"), AllowAutoRedirect=false, HandleCookies=false });
existingBrowser.DefaultRequestHeaders.Add("Cookie",loginCookie);
Check((await existingBrowser.GetAsync("/Employee/Sales/Create")).IsSuccessStatusCode,"existing authentication cookie survives host restart with persisted keys");
var durations = await Task.WhenAll(Enumerable.Range(0,50).Select(async _ =>
{
    var timer=System.Diagnostics.Stopwatch.StartNew();
    var response=await existingBrowser.GetAsync("/Employee/Sales/Create");
    if(!response.IsSuccessStatusCode) throw new Exception("Read burst failed");
    return timer.Elapsed.TotalMilliseconds;
}));
Check(durations.Length==50,"50 concurrent authenticated form reads complete");
Console.WriteLine($"LOCAL READ BURST p95={durations.Order().ElementAt(47):F1}ms; not a VPS capacity measurement");
Console.WriteLine($"TOTAL PASS: {checks}");

sealed class AuditFactory(string root, string keys, string connection) : WebApplicationFactory<RinconEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.Combine(root,"Rincon")).UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        builder.ConfigureAppConfiguration((_,config) => config.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["ConnectionStrings:ConexionPostgres"]=connection, ["DataProtection:KeysPath"]=keys,
            ["AllowedHosts"]="localhost", ["BootstrapAdmin:Email"]="audit@example.invalid",
            ["BootstrapAdmin:Password"]="Synthetic-Audit-Only!42",
            ["Logging:LogLevel:Default"]="Error"
        }));
    }
}
