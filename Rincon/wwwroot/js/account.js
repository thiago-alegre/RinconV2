$(function () {
    createServerDataTable("#accountsTable", {
        order: [[0, "asc"]],
        ajax: {
            url: "/Employee/Accounts/GetAll",
            type: "GET"
        },
        columns: [
            { data: "fullName" },
            { data: "dni" },
            {
                data: "phone",
                render: value => value || "-"
            },
            {
                data: "debt",
                render: debt => {
                    const debtClass = Number(debt) > 0 ? "text-danger" : "text-success";
                    return `<span class="fw-bold ${debtClass}">${formatAccountMoney(debt)}</span>`;
                }
            },
            {
                data: "debtSince",
                render: value => value || "-"
            },
            {
                data: "isActive",
                render: isActive => isActive
                    ? '<span class="status-badge status-active">Activa</span>'
                    : '<span class="status-badge status-inactive">Inactiva</span>'
            },
            {
                data: "id",
                orderable: false,
                render: accountId => `
                    <div class="datatable-action-group justify-content-end">
                        <a href="/Employee/Accounts/Detail/${accountId}"
                           class="btn btn-soft-secondary btn-modern-sm">
                            <i class="fa fa-eye me-1"></i> Detalle
                        </a>
                        <a href="/Employee/Accounts/Upsert/${accountId}"
                           class="btn btn-soft-secondary btn-modern-sm">
                            <i class="fa fa-pen me-1"></i> Editar
                        </a>
                    </div>`
            }
        ]
    });
});

function formatAccountMoney(value) {
    return Number(value ?? 0).toLocaleString("es-AR", {
        style: "currency",
        currency: "ARS"
    });
}
