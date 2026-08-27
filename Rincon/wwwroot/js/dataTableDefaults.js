window.createServerDataTable = function (selector, options) {
    const defaults = {
        processing: true,
        serverSide: true,
        pageLength: 5,
        lengthMenu: [5, 10, 25, 50],
        paging: true,
        searching: true,
        drawCallback: function () {
            const table = this.api();
            const hasMultiplePages = table.page.info().pages > 1;
            const container = $(table.table().container());

            container
                .find(".dt-paging, .dataTables_paginate")
                .toggle(hasMultiplePages);
        },
        language: {
            processing: "Procesando...",
            search: "Buscar:",
            lengthMenu: "Mostrar _MENU_ registros",
            info: "Mostrando _START_ a _END_ de _TOTAL_ registros",
            infoEmpty: "No hay registros para mostrar",
            infoFiltered: "(filtrado de _MAX_ registros)",
            emptyTable: "No hay registros disponibles",
            zeroRecords: "No se encontraron resultados",
            paginate: {
                first: "Primero",
                last: "Último",
                next: "Siguiente",
                previous: "Anterior"
            }
        }
    };

    const settings = $.extend(true, {}, defaults, options);
    return $(selector).DataTable(settings);
};
