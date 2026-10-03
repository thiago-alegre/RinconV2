using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;

namespace Rincon.Infrastructure;

public static class ProductionSafety
{
    public static void ConfigureProxy(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);
    }

    public static void CheckKeys(IServiceProvider services, string path)
    {
        Directory.CreateDirectory(path);
        var probe = Path.Combine(path, $".probe-{Guid.NewGuid():N}");
        try { File.WriteAllText(probe, "write-check"); }
        finally { if (File.Exists(probe)) File.Delete(probe); }
        var protector = services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("RinconV2.StartupProbe.v1");
        var value = Guid.NewGuid().ToString();
        if (protector.Unprotect(protector.Protect(value)) != value)
            throw new InvalidOperationException("Data Protection startup check failed.");
    }

    public static async Task WriteError(HttpContext context, int status)
    {
        context.Response.StatusCode = status;
        context.Response.Headers["X-Request-ID"] = context.TraceIdentifier;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        var id = WebUtility.HtmlEncode(context.TraceIdentifier);
        await context.Response.WriteAsync($"<!doctype html><html lang=\"es\"><meta charset=\"utf-8\"><title>Solicitud interrumpida</title><h1>No se pudo completar la solicitud ({status})</h1><p>Referencia: {id}</p><p>Si estabas registrando una operación, revisá el historial antes de volver a enviarla. Esta pantalla no confirma si se guardó.</p><p><a href=\"/Employee/Sales\">Ver ventas</a> · <a href=\"/Identity/Account/Login\">Iniciar sesión</a></p></html>");
    }

    public static void UseRequestEvidence(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var watch = Stopwatch.StartNew();
            context.Response.Headers["X-Request-ID"] = context.TraceIdentifier;
            try { await next(context); }
            catch (Exception exception) when (!context.Response.HasStarted && IsDatabaseConflict(exception))
            {
                // No automatic replay: the client must reconcile the outcome first.
                context.Response.Clear();
                context.Response.Headers["X-Request-ID"] = context.TraceIdentifier;
                await WriteError(context, 409);
            }
            finally
            {
                // Route pattern avoids query strings, user input and identifiers in paths.
                var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
                app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Rincon.Requests")
                    .LogInformation("Request {Method} {Route} {Status} {ElapsedMs}ms {RequestId}",
                        context.Request.Method, route, context.Response.StatusCode,
                        watch.ElapsedMilliseconds, context.TraceIdentifier);
            }
        });
    }

    public static bool IsDatabaseConflict(Exception exception) =>
        exception is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ||
        exception is PostgresException { SqlState: "40001" or "40P01" } ||
        exception is PostgresException { SqlState: "23505", ConstraintName: "IX_DirectSales_OperationId" or "IX_PersonalAccountPayments_OperationId" or "IX_DirectSaleReturns_OperationId" or "IX_Expenses_OperationId" } ||
        (exception.InnerException is not null && IsDatabaseConflict(exception.InnerException));
}
