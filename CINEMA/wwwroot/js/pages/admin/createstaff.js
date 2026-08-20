function previewImage(event) {
    const preview = document.getElementById('preview');
    if (preview && event.target.files && event.target.files[0]) {
        preview.src = URL.createObjectURL(event.target.files[0]);
    }
}
