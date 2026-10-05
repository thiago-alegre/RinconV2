using System.Net;
using Microsoft.EntityFrameworkCore;
using Rincon.DataAccess.Data;
using Rincon.Infrastructure;
using Rincon.Models;

static class CommercialChecks
{
    public static async Task Run(
        IServiceProvider services,
        HttpClient client,
        DbContextOptions<ApplicationDbContext> options,
        Func<string, string, string> field,
        Action<bool, string> check)
    {
        await using var db = new ApplicationDbContext(options);
        async Task<Dictionary<string, string>> Form(string url)
        {
            var html = await client.GetStringAsync(url);
            return new()
            {
                ["__RequestVerificationToken"] = field(html, "__RequestVerificationToken"),
                ["OperationId"] = field(html, "OperationId")
            };
        }

        Task<HttpResponseMessage> Post(string url, Dictionary<string, string> form) =>
            client.PostAsync(url, new FormUrlEncodedContent(form));

        var legacyDni = Guid.NewGuid().ToString();
        var legacyAccountForm = await Form("/Employee/Accounts/Upsert");
        legacyAccountForm["FullName"] = "Imported paper account";
        legacyAccountForm["DNI"] = legacyDni;
        legacyAccountForm["OpeningBalance"] = "125,50";
        legacyAccountForm["isActive"] = "true";
        check((await Post("/Employee/Accounts/Upsert", legacyAccountForm)).StatusCode == HttpStatusCode.Redirect,
            "account can be created with a previous balance");
        var legacyAccount = await db.PersonalAccounts.AsNoTracking().SingleAsync(item => item.DNI == legacyDni);
        check((await AccountLedger.Accounts(db).SingleAsync(item => item.Id == legacyAccount.Id)).DebtValue == 125.50m,
            "previous balance becomes debt without a synthetic sale");
        check(!await db.DirectSales.AnyAsync(item => item.PersonalAccountId == legacyAccount.Id),
            "previous balance does not create products or sales");

        var account = new PersonalAccount
        {
            FullName = "Cancellation regression",
            DNI = Guid.NewGuid().ToString(),
            OpeningBalance = 75,
            isActive = true
        };
        var shirt = new Product { Name = "Remera", SalePrice = 100, PurchasePrice = 40, Quantity = 50 };
        var blanket = new Product { Name = "Manta", SalePrice = 200, PurchasePrice = 90, Quantity = 50 };
        db.AddRange(account, shirt, blanket);
        await db.SaveChangesAsync();

        var productsBeforeLooseSale = await db.Products.CountAsync();
        var shirtStockBeforeLooseSale = shirt.Quantity;
        var looseSaleForm = await Form("/Employee/Sales/Create");
        looseSaleForm["PaymentMethod"] = "1";
        looseSaleForm["Lines[0].IsLoose"] = "true";
        looseSaleForm["Lines[0].LooseName"] = "Chicle suelto";
        looseSaleForm["Lines[0].LooseUnitPrice"] = "25,50";
        looseSaleForm["Lines[0].Quantity"] = "2";
        check((await Post("/Employee/Sales/Create", looseSaleForm)).StatusCode == HttpStatusCode.Redirect,
            "loose product sale is registered");
        var looseOperationId = Guid.Parse(looseSaleForm["OperationId"]);
        var looseSale = await db.DirectSales.AsNoTracking().Include(item => item.Items)
            .SingleAsync(item => item.OperationId == looseOperationId);
        var looseItem = looseSale.Items.Single();
        check(looseItem.ProductId is null && looseItem.ProductName == "Chicle suelto" &&
              looseItem.UnitPrice == 25.50m && looseItem.Quantity == 2 && looseSale.Total == 51m,
            "loose line preserves description price and quantity");
        await db.Entry(shirt).ReloadAsync();
        check(await db.Products.CountAsync() == productsBeforeLooseSale && shirt.Quantity == shirtStockBeforeLooseSale,
            "loose product neither creates a product nor changes stock");

        var looseVoidForm = await Form("/Employee/Sales/Void/" + looseSale.Id);
        looseVoidForm["SaleId"] = looseSale.Id.ToString();
        looseVoidForm["Lines[0].DirectSaleItemId"] = looseItem.Id.ToString();
        looseVoidForm["Lines[0].Selected"] = "true";
        looseVoidForm["Lines[0].Quantity"] = "2";
        looseVoidForm["Lines[0].ReturnsToStock"] = "true";
        check((await Post("/Employee/Sales/Void", looseVoidForm)).StatusCode == HttpStatusCode.Redirect,
            "loose product can be cancelled");
        var looseReturnOperationId = Guid.Parse(looseVoidForm["OperationId"]);
        var looseReturn = await db.DirectSaleReturns.AsNoTracking().Include(item => item.Items)
            .SingleAsync(item => item.OperationId == looseReturnOperationId);
        check(!looseReturn.Items.Single().ReturnsToStock,
            "loose product cancellation can never return stock");

        var saleForm = await Form("/Employee/Sales/Create");
        saleForm["PaymentMethod"] = "3";
        saleForm["PersonalAccountId"] = account.Id.ToString();
        saleForm["Lines[0].ProductId"] = shirt.Id.ToString();
        saleForm["Lines[0].Quantity"] = "2";
        saleForm["Lines[1].ProductId"] = blanket.Id.ToString();
        saleForm["Lines[1].Quantity"] = "2";
        check((await Post("/Employee/Sales/Create", saleForm)).StatusCode == HttpStatusCode.Redirect,
            "sale with multiple products is registered");

        var operationId = Guid.Parse(saleForm["OperationId"]);
        var sale = await db.DirectSales.AsNoTracking().Include(item => item.Items)
            .SingleAsync(item => item.OperationId == operationId);
        var shirtLine = sale.Items.Single(item => item.ProductId == shirt.Id);
        var blanketLine = sale.Items.Single(item => item.ProductId == blanket.Id);

        var accountProductsBeforeCancellation = await client.GetStringAsync(
            "/Employee/Accounts/GetSaleDetails?id=" + account.Id);
        check(accountProductsBeforeCancellation.Contains($"\"saleId\":{sale.Id}") &&
              accountProductsBeforeCancellation.Contains("\"canCancel\":true"),
            "account products expose their cancellable sale action");

        var paymentForm = await Form("/Employee/Accounts/Detail/" + account.Id);
        paymentForm["Id"] = account.Id.ToString();
        paymentForm["AmountText"] = "675";
        paymentForm["PaymentMethod"] = "1";
        check((await Post("/Employee/Accounts/Pay", paymentForm)).StatusCode == HttpStatusCode.Redirect,
            "personal account can be settled");

        var partial = await Form("/Employee/Sales/Void/" + sale.Id);
        partial["SaleId"] = sale.Id.ToString();
        partial["RefundMethod"] = "1"; // The server must force account adjustment.
        partial["Lines[0].DirectSaleItemId"] = shirtLine.Id.ToString();
        partial["Lines[0].Selected"] = "true";
        partial["Lines[0].Quantity"] = "1";
        partial["Lines[0].ReturnsToStock"] = "false";
        var beforeCancellations = await db.DirectSaleReturns.CountAsync();
        check((await Post("/Employee/Sales/Void", partial)).StatusCode == HttpStatusCode.Redirect,
            "partial cancellation is accepted");
        await Post("/Employee/Sales/Void", partial);
        check(await db.DirectSaleReturns.CountAsync() == beforeCancellations + 1,
            "cancellation retry is idempotent");

        var savedPartial = await db.DirectSaleReturns.Include(item => item.Items)
            .SingleAsync(item => item.OperationId == Guid.Parse(partial["OperationId"]));
        check(savedPartial.RefundMethod == Rincon.Utilities.Enums.PaymentMethod.CuentaPersonal,
            "account cancellation cannot be converted into a cash refund");
        check(savedPartial.Items.Single().ReturnsToStock == false,
            "an item can be cancelled without returning to stock");
        await db.Entry(shirt).ReloadAsync();
        check(shirt.Quantity == 48, "stock remains unchanged when restock is unchecked");

        var complete = await Form("/Employee/Sales/Void/" + sale.Id);
        complete["SaleId"] = sale.Id.ToString();
        complete["Lines[0].DirectSaleItemId"] = shirtLine.Id.ToString();
        complete["Lines[0].Selected"] = "true";
        complete["Lines[0].Quantity"] = "1";
        complete["Lines[0].ReturnsToStock"] = "true";
        complete["Lines[1].DirectSaleItemId"] = blanketLine.Id.ToString();
        complete["Lines[1].Selected"] = "true";
        complete["Lines[1].Quantity"] = "2";
        complete["Lines[1].ReturnsToStock"] = "true";
        check((await Post("/Employee/Sales/Void", complete)).StatusCode == HttpStatusCode.Redirect,
            "selecting every remaining item completes the cancellation");

        await db.Entry(shirt).ReloadAsync();
        await db.Entry(blanket).ReloadAsync();
        check(shirt.Quantity == 49 && blanket.Quantity == 50,
            "each cancelled line follows its own restock selection");
        var balance = await AccountLedger.Accounts(db).SingleAsync(item => item.Id == account.Id);
        check(balance.DebtValue == 0,
            "a settled account remains at zero without credit after cancellation");

        var history = await client.GetStringAsync("/Employee/Accounts/GetCancellations?id=" + account.Id);
        check(history.Contains("Remera") && history.Contains("Manta"),
            "account history exposes cancellation movements");
        var salesHistory = await client.GetStringAsync("/Employee/Sales/GetAll?status=voided");
        check(salesHistory.Contains("Anulada"), "fully cancelled sale appears as cancelled");
        var accountProductsAfterCancellation = await client.GetStringAsync(
            "/Employee/Accounts/GetSaleDetails?id=" + account.Id);
        check(accountProductsAfterCancellation.Contains($"\"saleId\":{sale.Id}") &&
              accountProductsAfterCancellation.Contains("\"canCancel\":false"),
            "account products disable cancellation after the whole sale is cancelled");
    }
}
