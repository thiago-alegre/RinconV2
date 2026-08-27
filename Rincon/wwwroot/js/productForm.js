document.addEventListener("DOMContentLoaded", () => {
    const imageInput = document.getElementById("image");
    const previewImage = document.getElementById("productPreviewImage");
    const previewPlaceholder = document.getElementById("productPreviewPlaceholder");

    if (!imageInput || !previewImage || !previewPlaceholder) {
        return;
    }

    const maxImageSize = 2 * 1024 * 1024;
    const allowedImageTypes = new Set([
        "image/jpeg",
        "image/png",
        "image/webp"
    ]);
    const originalImageUrl = previewImage.dataset.originalSrc ?? "";
    let temporaryImageUrl = null;

    function releaseTemporaryImage() {
        if (!temporaryImageUrl) {
            return;
        }

        URL.revokeObjectURL(temporaryImageUrl);
        temporaryImageUrl = null;
    }

    function showImage(imageUrl, alternativeText) {
        previewImage.src = imageUrl;
        previewImage.alt = alternativeText;
        previewImage.classList.remove("d-none");
        previewPlaceholder.classList.add("d-none");
    }

    function showOriginalImage() {
        releaseTemporaryImage();

        if (originalImageUrl) {
            showImage(originalImageUrl, "Foto actual del producto");
            return;
        }

        previewImage.removeAttribute("src");
        previewImage.classList.add("d-none");
        previewPlaceholder.classList.remove("d-none");
    }

    function validateImage(file) {
        if (!allowedImageTypes.has(file.type)) {
            return "La imagen debe estar en formato JPG, PNG o WEBP.";
        }

        if (file.size > maxImageSize) {
            return "La imagen no puede superar los 2 MB.";
        }

        return "";
    }

    imageInput.addEventListener("change", () => {
        const image = imageInput.files?.[0];

        if (!image) {
            imageInput.setCustomValidity("");
            showOriginalImage();
            return;
        }

        const validationMessage = validateImage(image);
        imageInput.setCustomValidity(validationMessage);

        if (validationMessage) {
            showOriginalImage();
            imageInput.reportValidity();
            return;
        }

        releaseTemporaryImage();
        temporaryImageUrl = URL.createObjectURL(image);
        showImage(temporaryImageUrl, `Vista previa de ${image.name}`);
    });

    window.addEventListener("beforeunload", releaseTemporaryImage);
});
