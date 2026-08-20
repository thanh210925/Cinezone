$(document).ready(function () {
    const movieSelect = $('#movieSelect');
    if (movieSelect.length && movieSelect.select2) {
        movieSelect.select2({
            placeholder: "🔍 Tìm và chọn phim...",
            allowClear: true,
            width: '100%',
            closeOnSelect: false
        });
    }
});
