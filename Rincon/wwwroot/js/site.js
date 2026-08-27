function rinconConfirm(options) {
    return Swal.fire({
        title: options.title,
        text: options.text,
        icon: options.icon || "question",
        showCancelButton: true,
        confirmButtonText: options.confirmButtonText || "Sí, continuar",
        cancelButtonText: options.cancelButtonText || "Cancelar",
        buttonsStyling: false,
        customClass: {
            popup: "rincon-swal-popup",
            title: "rincon-swal-title",
            htmlContainer: "rincon-swal-text",
            actions: "rincon-swal-actions",
            icon: "rincon-swal-icon",
            confirmButton: "rincon-swal-confirm",
            cancelButton: "rincon-swal-cancel"
        }
    });
}

if (window.jQuery) {
    $.ajaxSetup({
        beforeSend: function (xhr, settings) {
            const method = (settings.type || settings.method || "GET").toUpperCase();

            if (!["POST", "PUT", "PATCH", "DELETE"].includes(method)) {
                return;
            }

            const token = document.querySelector('meta[name="request-verification-token"]')?.getAttribute("content");

            if (token) {
                xhr.setRequestHeader("RequestVerificationToken", token);
            }
        }
    });
}

function rinconModal(options) {
    const config = {
        title: options.title,
        text: options.text,
        icon: options.icon || "info",
        confirmButtonText: options.confirmButtonText || "Entendido",
        buttonsStyling: false,
        customClass: {
            popup: "rincon-swal-popup",
            title: "rincon-swal-title",
            htmlContainer: "rincon-swal-text",
            actions: "rincon-swal-actions",
            icon: "rincon-swal-icon",
            confirmButton: "rincon-swal-confirm"
        }
    };

    if (options.showCancelButton) {
        config.showCancelButton = true;
        config.cancelButtonText = options.cancelButtonText || "Cancelar";
        config.customClass.cancelButton = "rincon-swal-cancel";
    }

    return Swal.fire(config).then(function (result) {
        if (result.isConfirmed && options.confirmUrl) {
            window.location.href = options.confirmUrl;
        }

        return result;
    });
}

function initializeFittedText() {
    const elements = document.querySelectorAll("[data-fit-text]");

    if (!elements.length) {
        return;
    }

    const fitElement = element => {
        const maximumSize = Number(element.dataset.fitTextMax) || 40;
        const minimumSize = Number(element.dataset.fitTextMin) || 16;

        element.style.fontSize = `${maximumSize}px`;

        if (element.scrollWidth <= element.clientWidth) {
            return;
        }

        const availableRatio = element.clientWidth / element.scrollWidth;
        const fittedSize = Math.max(minimumSize, Math.floor(maximumSize * availableRatio));

        element.style.fontSize = `${fittedSize}px`;
    };

    const fitAll = () => elements.forEach(fitElement);

    fitAll();

    if ("ResizeObserver" in window) {
        const observer = new ResizeObserver(fitAll);
        elements.forEach(element => observer.observe(element));
        return;
    }

    window.addEventListener("resize", fitAll);
}

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initializeFittedText);
} else {
    initializeFittedText();
}
