let productsTable;

$(function () {
    productsTable = createServerDataTable("#tblArticles", {
        order: [[1, "asc"]],
        ajax: {
            url: "/Admin/Articles/GetAll",
            type: "GET"
        },
        columns: [
            {
                data: "imageUrl",
                orderable: false,
                render: renderProductImage
            },
            { data: "name" },
            {
                data: "quantity",
                render: renderQuantity
            },
            {
                data: "purchasePrice",
                render: formatMoney
            },
            {
                data: "salePrice",
                render: formatMoney
            },
            {
                data: null,
                render: product => formatMoney(product.salePrice - product.purchasePrice)
            },
            {
                data: "isActive",
                render: renderStatus
            },
            {
                data: "id",
                orderable: false,
                render: renderActions
            }
        ]
    });
});

function renderProductImage(imageUrl) {
    if (!imageUrl) {
        return '<span class="text-muted">Sin foto</span>';
    }

    return `<img src="${imageUrl}" class="product-img-sm" alt="Foto del producto" />`;
}

function renderQuantity(quantity) {
    const value = Number(quantity ?? 0).toLocaleString("es-AR");
    return `${value} unidades`;
}

function formatMoney(value) {
    return Number(value ?? 0).toLocaleString("es-AR", {
        style: "currency",
        currency: "ARS"
    });
}

function renderStatus(isActive) {
    return isActive
        ? '<span class="status-badge status-active">Activo</span>'
        : '<span class="status-badge status-inactive">Inactivo</span>';
}

function renderActions(productId) {
    return `
        <div class="datatable-action-group">
            <a href="/Admin/Articles/Upsert/${productId}"
               class="btn btn-soft-secondary btn-modern-sm"
               title="Editar producto">
                <i class="fa fa-pencil"></i>
            </a>
            <button type="button"
                    onclick="deactivateProduct(${productId})"
                    class="btn btn-soft-danger btn-modern-sm"
                    title="Desactivar producto">
                <i class="fa fa-ban"></i>
            </button>
        </div>`;
}

function deactivateProduct(productId) {
    rinconConfirm({
        title: "Desactivar producto",
        text: "El producto dejará de estar disponible para nuevas ventas.",
        confirmButtonText: "Desactivar",
        cancelButtonText: "Cancelar"
    }).then(result => {
        if (!result.isConfirmed) {
            return;
        }

        $.ajax({
            type: "DELETE",
            url: `/Admin/Articles/Delete/${productId}`,
            success: response => {
                if (response.success) {
                    toastr.success(response.message);
                    productsTable.ajax.reload();
                    return;
                }

                toastr.error(response.message);
            },
            error: () => toastr.error("No se pudo desactivar el producto")
        });
    });
}
