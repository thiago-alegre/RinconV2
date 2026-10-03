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
                    const paymentDetail = sale.paymentBreakdown ||
                        (sale.account && sale.account !== "-" ? sale.account : "");
                    const detail = paymentDetail
                        ? `<small class="d-block text-muted">${escapeSalesHistoryHtml(paymentDetail)}</small>`
                        : "";

                    return `<span class="fw-semibold">${escapeSalesHistoryHtml(sale.paymentMethod)}</span>${detail}`;
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
                data: "returnStatus",
                render: status => {
                    const css = status === "Vigente" ? "status-active" : status === "Anulación parcial" ? "status-warning" : "status-inactive";
                    return `<span class="status-badge ${css}">${escapeSalesHistoryHtml(status)}</span>`;
                }
            },
            {
                data: "detailUrl",
                orderable: false,
                className: "text-end",
                render: detailUrl => `
                    <a href="${detailUrl}" class="btn btn-soft-secondary btn-modern-sm">
                        Detalle
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
