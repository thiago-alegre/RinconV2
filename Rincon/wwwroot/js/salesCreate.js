(() => {
    "use strict";

    const linesContainer = document.getElementById("lines");
    const addLineButton = document.getElementById("addLine");
    const addLooseLineButton = document.getElementById("addLooseLine");
    const looseLineTemplate = document.getElementById("looseLineTemplate");
    const totalElement = document.getElementById("total");
    const saleForm = document.querySelector(".sale-create-form");
    const paymentSelect = document.getElementById("payment");
    const accountBox = document.getElementById("accountBox");
    const combinedPaymentFields = document.getElementById("combinedPaymentFields");
    const cashAmountInput = document.getElementById("cashAmount");
    const transferAmountInput = document.getElementById("transferAmount");
    const combinedPaymentFeedback = document.getElementById("combinedPaymentFeedback");
    let combinedPaymentTouched = Boolean(combinedPaymentFeedback?.textContent.trim());

    if (!linesContainer || !addLineButton || !totalElement) {
        return;
    }

    const moneyFormatter = new Intl.NumberFormat("es-AR", {
        style: "currency",
        currency: "ARS"
    });

    function initializeProductSearch(selectElement) {
        const select = $(selectElement);

        if (select.hasClass("select2-hidden-accessible")) {
            return;
        }

        select.select2({
            theme: "bootstrap-5",
            width: "100%",
            placeholder: "Buscar un producto...",
            allowClear: true,
            minimumResultsForSearch: 0,
            language: {
                noResults: () => "No se encontraron productos"
            }
        });
    }

    function initializeAccountSearch() {
        const accountSelect = $("#personalAccount");

        if (!accountSelect.length || accountSelect.hasClass("select2-hidden-accessible")) {
            return;
        }

        accountSelect.select2({
            theme: "bootstrap-5",
            width: "100%",
            placeholder: "Buscar una cuenta personal...",
            allowClear: true,
            minimumResultsForSearch: 0,
            language: {
                noResults: () => "No se encontraron cuentas personales"
            }
        });
    }

    function parseMoney(value) {
        let normalized = String(value ?? "")
            .trim()
            .replace(/\$/g, "")
            .replace(/\s/g, "");

        if (!normalized) {
            return 0;
        }

        if (normalized.includes(",")) {
            normalized = normalized.replace(/\./g, "").replace(",", ".");
        }

        const amount = Number(normalized);
        return Number.isFinite(amount) ? amount : Number.NaN;
    }

    function getSaleTotal() {
        let total = 0;

        linesContainer.querySelectorAll(".sale-line").forEach(line => {
            const quantity = line.querySelector(".quantity");
            const product = line.querySelector(".product");
            const loosePrice = line.querySelector(".loose-price");
            const price = loosePrice
                ? parseMoney(loosePrice.value)
                : Number(product?.selectedOptions[0]?.dataset.price ?? 0);
            const units = Number(quantity?.value ?? 0);

            if (Number.isFinite(price) && Number.isFinite(units)) {
                total += price * units;
            }
        });

        return total;
    }

    function validateQuantity(quantityInput) {
        const quantity = Number(quantityInput.value);
        const isValid = Number.isInteger(quantity) && quantity >= 1;

        quantityInput.setCustomValidity(
            isValid ? "" : "La cantidad debe ser un número entero mayor a cero."
        );

        return isValid;
    }

    function validateLooseLine(line) {
        const name = line.querySelector(".loose-name");
        const price = line.querySelector(".loose-price");

        if (!name || !price) {
            return true;
        }

        const validName = name.value.trim().length > 0;
        const amount = parseMoney(price.value);
        const validPrice = Number.isFinite(amount) && amount > 0;
        name.setCustomValidity(validName ? "" : "Ingresá una descripción para el producto suelto.");
        price.setCustomValidity(validPrice ? "" : "Ingresá un precio mayor a cero.");
        return validName && validPrice;
    }

    function validateCombinedPayment(total) {
        if (!cashAmountInput || !transferAmountInput || !combinedPaymentFeedback) {
            return;
        }

        cashAmountInput.setCustomValidity("");
        transferAmountInput.setCustomValidity("");
        combinedPaymentFeedback.textContent = "";
        combinedPaymentFeedback.classList.remove("is-valid", "is-invalid");

        if (!usesCombinedPayment() || !combinedPaymentTouched) {
            return true;
        }

        const cashAmount = parseMoney(cashAmountInput.value);
        const transferAmount = parseMoney(transferAmountInput.value);

        if (!Number.isFinite(cashAmount) || cashAmount <= 0) {
            cashAmountInput.setCustomValidity("Ingrese un monto en efectivo mayor a cero.");
            combinedPaymentFeedback.textContent = "Ingresá un monto en efectivo mayor a cero.";
            combinedPaymentFeedback.classList.add("is-invalid");
            return false;
        }

        if (!Number.isFinite(transferAmount) || transferAmount <= 0) {
            transferAmountInput.setCustomValidity("Ingrese un monto en transferencia mayor a cero.");
            combinedPaymentFeedback.textContent = "Ingresá un monto en transferencia mayor a cero.";
            combinedPaymentFeedback.classList.add("is-invalid");
            return false;
        }

        const difference = Math.abs(cashAmount + transferAmount - total);

        if (difference >= 0.005) {
            transferAmountInput.setCustomValidity("Los importes deben coincidir con el total de la venta.");
            combinedPaymentFeedback.textContent =
                `La suma debe coincidir con el total de la venta (${moneyFormatter.format(total)}).`;
            combinedPaymentFeedback.classList.add("is-invalid");
            return false;
        }

        return true;
    }

    function updateTotal() {
        const total = getSaleTotal();
        totalElement.textContent = moneyFormatter.format(total);
        validateCombinedPayment(total);
    }

    function updateAccountVisibility() {
        if (!accountBox || !paymentSelect) {
            return;
        }

        const usesPersonalAccount = paymentSelect.value === "3" ||
            paymentSelect.value === "CuentaPersonal";

        accountBox.hidden = !usesPersonalAccount;
    }

    function usesCombinedPayment() {
        return paymentSelect?.value === "4" || paymentSelect?.value === "Combinado";
    }

    function updatePaymentVisibility() {
        const isCombined = usesCombinedPayment();

        if (combinedPaymentFields) {
            combinedPaymentFields.hidden = !isCombined;
        }

        if (cashAmountInput) {
            cashAmountInput.disabled = !isCombined;
        }

        if (transferAmountInput) {
            transferAmountInput.disabled = !isCombined;
        }

        updateAccountVisibility();
        validateCombinedPayment(getSaleTotal());
    }

    function sanitizeClonedProductSelect(line) {
        line.querySelectorAll(".select2-container").forEach(container => container.remove());

        const productSelect = line.querySelector(".product");
        productSelect.classList.remove("select2-hidden-accessible");
        productSelect.removeAttribute("data-select2-id");
        productSelect.removeAttribute("aria-hidden");
        productSelect.removeAttribute("tabindex");
        productSelect.value = "";

        productSelect.querySelectorAll("[data-select2-id]")
            .forEach(option => option.removeAttribute("data-select2-id"));

        return productSelect;
    }

    function renumberLines() {
        linesContainer.querySelectorAll(".sale-line").forEach((line, index) => {
            const fields = {
                ".line-is-loose": "IsLoose",
                ".product": "ProductId",
                ".loose-name": "LooseName",
                ".loose-price": "LooseUnitPrice",
                ".quantity": "Quantity"
            };

            Object.entries(fields).forEach(([selector, field]) => {
                const input = line.querySelector(selector);
                if (input) input.name = `Lines[${index}].${field}`;
            });
        });

        updateTotal();
    }

    addLineButton.addEventListener("click", () => {
        const sourceLine = linesContainer.querySelector(".sale-line-registered");

        if (!sourceLine) {
            return;
        }

        const newLine = sourceLine.cloneNode(true);
        const productSelect = sanitizeClonedProductSelect(newLine);
        const quantityInput = newLine.querySelector(".quantity");
        quantityInput.value = "1";
        validateQuantity(quantityInput);
        linesContainer.appendChild(newLine);
        initializeProductSearch(productSelect);
        renumberLines();
    });

    addLooseLineButton?.addEventListener("click", () => {
        if (!looseLineTemplate) {
            return;
        }

        const index = linesContainer.querySelectorAll(".sale-line").length;
        const wrapper = document.createElement("div");
        wrapper.innerHTML = looseLineTemplate.innerHTML.replaceAll("__index__", String(index)).trim();
        const newLine = wrapper.firstElementChild;
        linesContainer.appendChild(newLine);
        validateQuantity(newLine.querySelector(".quantity"));
        renumberLines();
        newLine.querySelector(".loose-name")?.focus();
    });

    linesContainer.addEventListener("click", event => {
        const removeButton = event.target.closest(".remove");

        if (!removeButton || linesContainer.children.length <= 1) {
            return;
        }

        const line = removeButton.closest(".sale-line");
        if (line.classList.contains("sale-line-registered") &&
            linesContainer.querySelectorAll(".sale-line-registered").length <= 1) {
            const product = $(line.querySelector(".product"));
            product.val("").trigger("change");
            line.querySelector(".quantity").value = "1";
            updateTotal();
            return;
        }

        const productElement = line.querySelector(".product");
        const productSelect = productElement ? $(productElement) : null;

        if (productSelect?.hasClass("select2-hidden-accessible")) {
            productSelect.select2("destroy");
        }

        line.remove();
        renumberLines();
    });

    linesContainer.addEventListener("input", event => {
        if (event.target.classList.contains("quantity")) {
            validateQuantity(event.target);
        }

        const looseLine = event.target.closest(".sale-line-loose");
        if (looseLine) validateLooseLine(looseLine);

        updateTotal();
    });
    $(linesContainer).on("change", ".product", updateTotal);
    paymentSelect?.addEventListener("change", () => {
        combinedPaymentTouched = false;
        if (usesCombinedPayment()) {
            if (cashAmountInput && parseMoney(cashAmountInput.value) === 0) {
                cashAmountInput.value = "";
            }
            if (transferAmountInput && parseMoney(transferAmountInput.value) === 0) {
                transferAmountInput.value = "";
            }
        }
        updatePaymentVisibility();
    });
    cashAmountInput?.addEventListener("input", () => {
        combinedPaymentTouched = true;
        updateTotal();
    });
    transferAmountInput?.addEventListener("input", () => {
        combinedPaymentTouched = true;
        updateTotal();
    });
    saleForm?.addEventListener("submit", event => {
        const quantitiesValid = [...linesContainer.querySelectorAll(".quantity")]
            .every(validateQuantity);
        const looseLinesValid = [...linesContainer.querySelectorAll(".sale-line-loose")]
            .every(validateLooseLine);
        let paymentValid = true;
        if (usesCombinedPayment()) {
            combinedPaymentTouched = true;
            paymentValid = validateCombinedPayment(getSaleTotal());
        }

        if (!quantitiesValid || !looseLinesValid || !paymentValid) {
            event.preventDefault();
            saleForm.reportValidity();
        }
    });

    linesContainer.querySelectorAll(".product").forEach(initializeProductSearch);
    linesContainer.querySelectorAll(".quantity").forEach(validateQuantity);
    initializeAccountSearch();
    renumberLines();
    updatePaymentVisibility();
})();
