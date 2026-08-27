let salesTable;

$(function () {
    salesTable = createServerDataTable("#salesTable", {
        order: [[0, "desc"]],
        ajax: {
            url: "/Employee/Sales/GetAll",
            type: "GET",
            data: request => {
                request.dateFrom = $("#salesDateFrom").val();
                request.dateTo = $("#salesDateTo").val();
                request.status = $("#salesStatus").val();
            }
        },
        columns: [
            { data: "date" },
            {
                data: "id",
                render: id => `<span class="fw-bold">#${id}</span>`
            },
            {
                data: "products",
                orderable: false,
                render: products => {
                    const text = escapeSalesHistoryHtml(products || "Sin productos");
                    return `<span class="sale-product-summary" title="${text}">${text}</span>`;
                }
            },
            {
                data: null,
                render: sale => {
                    const account = sale.account && sale.account !== "-"
                        ? `<small class="d-block text-muted">${escapeSalesHistoryHtml(sale.account)}</small>`
                        : "";

                    return `<span class="fw-semibold">${escapeSalesHistoryHtml(sale.paymentMethod)}</span>${account}`;
                }
            },
            {
                data: "total",
                className: "text-end fw-bold",
                render: formatSalesHistoryMoney
            },
            {
                data: "profit",
                className: "text-end",
                render: value => `<span class="${value >= 0 ? "text-success" : "text-danger"}">${formatSalesHistoryMoney(value)}</span>`
            },
            { data: "user" },
            {
                data: "isVoided",
                render: isVoided => isVoided
                    ? '<span class="status-badge status-inactive">Anulada</span>'
                    : '<span class="status-badge status-active">Vigente</span>'
            },
            {
                data: "detailUrl",
                orderable: false,
                className: "text-end",
                render: detailUrl => `
                    <a href="${detailUrl}" class="btn btn-soft-secondary btn-modern-sm">
                        <i class="bi bi-eye me-1"></i> Detalle
                    </a>`
            }
        ]
    });

    $("#salesPeriodForm").on("submit", event => {
        event.preventDefault();
        salesTable.ajax.reload();
    });
});

function formatSalesHistoryMoney(value) {
    return Number(value ?? 0).toLocaleString("es-AR", {
        style: "currency",
        currency: "ARS"
    });
}

function escapeSalesHistoryHtml(value) {
    return $("<div>").text(value ?? "").html();
}
