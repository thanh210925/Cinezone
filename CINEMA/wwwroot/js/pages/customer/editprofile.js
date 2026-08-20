function preview(event) {
    var fileInput = event.target;
    if (fileInput.files && fileInput.files[0]) {
        var reader = new FileReader();
        reader.onload = function () {
            const previewImg = document.getElementById('previewImg');
            if (previewImg) previewImg.src = reader.result;
        };
        reader.readAsDataURL(fileInput.files[0]);
    }
}
