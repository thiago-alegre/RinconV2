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
            { data: "notes" }
        ]
    });
});

function formatAccountDetailMoney(value) {
    return Number(value ?? 0).toLocaleString("es-AR", {
        style: "currency",
        currency: "ARS"
    });
}
