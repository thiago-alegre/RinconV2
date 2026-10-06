$(function () {
    const accountId = $("#accountSaleDetailsTable").data("account-id");

    createServerDataTable("#accountSaleDetailsTable", {
        order: [[0, "desc"]],
        ajax: {
            url: "/Employee/Accounts/GetSaleDetails",
            type: "GET",
            data: request => {
                request.id = accountId;
            }
        },
        columns: [
            { data: "date" },
            { data: "product" },
            { data: "quantity" },
            {
                data: "unitPrice",
                render: formatAccountDetailMoney
            },
            {
                data: "subtotal",
                render: value => `<span class="fw-bold">${formatAccountDetailMoney(value)}</span>`
            },
            {
                data: "status",
                orderable: false,
                render: (status, type, row) =>
                    `<span class="status-badge ${row.statusClass}">${status}</span>`
            },
            {
                data: null,
                orderable: false,
                searchable: false,
                render: (_value, type, row) => {
                    if (type !== "display") return "";

                    const saleId = Number(row.saleId);
                    if (!row.canCancel || !Number.isInteger(saleId) || saleId <= 0) {
                        return '<span class="text-muted">Sin acciones</span>';
                    }

                    return `<a class="btn btn-outline-danger btn-sm" href="/Employee/Sales/Void/${saleId}" aria-label="Anular productos de la venta ${saleId}">
                        <i class="fa fa-ban me-1" aria-hidden="true"></i>Anular
                    </a>`;
                }
            }
        ]
    });

    createServerDataTable("#accountPaymentsTable", {
        order: [[0, "desc"]],
        ajax: {
            url: "/Employee/Accounts/GetPayments",
            type: "GET",
            data: request => {
                request.id = accountId;
            }
        },
        columns: [
            { data: "date" },
            { data: "paymentMethod" },
            {
                data: "amount",
                render: value => `<span class="fw-bold text-success">${formatAccountDetailMoney(value)}</span>`
            },
            { data: "user" },
            {
                data: "notes",
                render: (value, type, row) => type === "display" && row.voidReason
                    ? `${escapeAccountDetailHtml(value)}<div class="small text-danger">${escapeAccountDetailHtml(row.voidReason)}</div>`
                    : type === "display" ? escapeAccountDetailHtml(value) : value
            },
            {
                data: "status",
                orderable: false,
                render: (value, type, row) => type === "display"
                    ? `<span class="status-badge ${row.statusClass}">${value}</span>`
                    : value
            },
            {
                data: null,
                orderable: false,
                searchable: false,
                render: (_value, type, row) => {
                    if (type !== "display") return "";
                    const paymentId = Number(row.id);
                    if (!row.canManage || !Number.isInteger(paymentId) || paymentId <= 0) {
                        return '<span class="text-muted">Sin acciones</span>';
                    }
                    return `<div class="d-flex gap-1">
                        <a class="btn btn-outline-primary btn-sm" href="/Employee/Accounts/EditPayment/${paymentId}">Modificar</a>
                        <a class="btn btn-outline-danger btn-sm" href="/Employee/Accounts/VoidPayment/${paymentId}">Anular</a>
                    </div>`;
                }
            }
        ]
    });

    createServerDataTable("#accountCancellationsTable", {
        order: [[0, "desc"]],
        ajax: {
            url: "/Employee/Accounts/GetCancellations",
            type: "GET",
            data: request => { request.id = accountId; }
        },
        columns: [
            { data: "date" },
            { data: "sale" },
            { data: "products" },
            { data: "refundMethod" },
            { data: "total", render: value => `<span class="fw-bold text-danger">-${formatAccountDetailMoney(value)}</span>` },
            { data: "user" },
            { data: "reason" }
        ]
    });
});

function formatAccountDetailMoney(value) {
    return Number(value ?? 0).toLocaleString("es-AR", {
        style: "currency",
        currency: "ARS"
    });
}

function escapeAccountDetailHtml(value) {
    return $("<div>").text(value ?? "").html();
}
