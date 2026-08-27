let expensesTable;

$(function () {
    expensesTable = createServerDataTable("#expensesTable", {
        order: [[0, "desc"]],
        ajax: {
            url: "/Employee/Expenses/GetAll",
            type: "GET",
            data: request => {
                request.dateFrom = $("#expenseDateFrom").val();
                request.dateTo = $("#expenseDateTo").val();
            },
            dataSrc: response => {
                $("#expensesTotal").text(`Total: ${formatExpenseMoney(response.totalAmount)}`);
                return response.data;
            }
        },
        columns: [
            { data: "date" },
            { data: "type" },
            {
                data: null,
                render: expense => {
                    const notes = expense.notes
                        ? `<div class="small text-muted">${escapeHtml(expense.notes)}</div>`
                        : "";

                    return `<strong>${escapeHtml(expense.concept)}</strong>${notes}`;
                }
            },
            { data: "paymentMethod" },
            {
                data: "amount",
                className: "text-end fw-bold text-danger",
                render: amount => `- ${formatExpenseMoney(amount)}`
            },
            {
                data: null,
                orderable: false,
                className: "text-end",
                render: expense => `
                    <div class="datatable-action-group justify-content-end">
                        <a href="/Employee/Expenses/Edit/${expense.id}"
                           class="btn btn-soft-secondary btn-modern-sm"
                           title="Editar movimiento">
                            <i class="bi bi-pencil me-1"></i> Editar
                        </a>
                        <button type="button"
                                class="btn btn-soft-danger btn-modern-sm"
                                title="Anular movimiento"
                                onclick="voidExpense(${expense.id})">
                            <i class="bi bi-x-circle me-1"></i> Anular
                        </button>
                    </div>`
            }
        ]
    });

    $("#expensePeriodForm").on("submit", event => {
        event.preventDefault();
        expensesTable.ajax.reload();
    });
});

function voidExpense(expenseId) {
    rinconConfirm({
        title: "¿Anular este movimiento?",
        text: "El movimiento quedará conservado en el historial.",
        confirmButtonText: "Anular",
        cancelButtonText: "Cancelar"
    }).then(result => {
        if (!result.isConfirmed) {
            return;
        }

        $.ajax({
            url: "/Employee/Expenses/Void",
            type: "POST",
            data: { id: expenseId },
            success: response => {
                if (!response.success) {
                    toastr.error(response.message);
                    return;
                }

                toastr.success(response.message);
                expensesTable.ajax.reload(null, false);
            },
            error: () => toastr.error("No se pudo anular el movimiento")
        });
    });
}

function formatExpenseMoney(value) {
    return Number(value ?? 0).toLocaleString("es-AR", {
        style: "currency",
        currency: "ARS"
    });
}

function escapeHtml(value) {
    return $("<div>").text(value ?? "").html();
}
