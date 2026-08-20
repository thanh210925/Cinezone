document.addEventListener('DOMContentLoaded', function () {
    const modal = document.getElementById('trailerModal');
    const frame = document.getElementById('trailerFrame');
    const title = document.getElementById('trailerLabel');

    if (modal && frame && title) {
        modal.addEventListener('show.bs.modal', function (event) {
            const button = event.relatedTarget;
            if (!button) return;
            const trailerUrl = button.getAttribute('data-trailer') || '';
            const movieTitle = button.getAttribute('data-title') || '';
            let embedUrl = "";

            if (trailerUrl.includes("youtube.com") || trailerUrl.includes("youtu.be")) {
                let videoId = "";
                if (trailerUrl.includes("v=")) videoId = trailerUrl.split("v=")[1].split("&")[0];
                else if (trailerUrl.includes("youtu.be/")) videoId = trailerUrl.split("youtu.be/")[1].split("?")[0];
                embedUrl = `https://www.youtube.com/embed/${videoId}?autoplay=1`;
            } else if (trailerUrl.endsWith(".mp4")) {
                embedUrl = trailerUrl;
            } else {
                embedUrl = trailerUrl;
            }

            frame.src = embedUrl;
            title.textContent = `🎥 Trailer – ${movieTitle}`;
        });

        modal.addEventListener('hidden.bs.modal', function () {
            frame.src = "";
        });
    }
});
