document.addEventListener("DOMContentLoaded", function () {
    const firstCard = document.querySelector(".theater-card");
    if (firstCard)
        firstCard.classList.add("active");

    const popupElement = document.getElementById("promoPopup");

    if (popupElement && window.bootstrap && window.bootstrap.Modal) {
        const popup = new bootstrap.Modal(popupElement);
        if (!sessionStorage.getItem("popupShown")) {
            setTimeout(() => popup.show(), 1000);
            sessionStorage.setItem("popupShown", "true");
        }
    }

    const list = document.getElementById("recommendList");

    if (list) {
        function renderMovieCard(movie, rank) {
            const col = document.createElement("div");
            col.className = "col";
            const ageRating = movie.ageRating || "P";
            const duration = movie.duration || 0;
            const posterSrc = (movie.posterUrl && movie.posterUrl.trim() !== '') ? movie.posterUrl : '/images/unnamed.jpg';

            const topBadgeHtml = rank <= 3 ? `<div class="top-badge top-${rank}">TOP ${rank}</div>` : '';

            col.innerHTML = `
                <div class="movie-card shadow-sm border rounded-4 overflow-hidden position-relative h-100">
                    ${topBadgeHtml}
                    <div class="age-rating-badge">${ageRating}</div>
                    <div class="movie-poster-container" style="height: 350px; min-height: 350px; background: #1e293b;">
                        <img src="${posterSrc}"
                             onerror="this.onerror=null; this.src='/images/unnamed.jpg';"
                             class="w-100 d-block"
                             style="height:350px; object-fit:cover;">
                        <div class="movie-overlay">
                            <div class="overlay-content text-center p-2">
                                <div class="movie-meta mb-3">
                                    <span class="badge bg-secondary me-2">${ageRating}</span>
                                    <span class="text-white">${duration} phút</span>
                                </div>
                                <div class="d-flex justify-content-center gap-2">
                                    <a href="/Home/MovieDetails/${movie.movieId}"
                                       class="btn btn-outline-light rounded-pill px-3 py-1 btn-sm fw-semibold">
                                        Chi tiết
                                    </a>
                                    <a href="/Home/BookTicket/${movie.movieId}"
                                       class="btn btn-danger rounded-pill px-3 py-1 btn-sm fw-semibold shadow-sm">
                                        Đặt vé
                                    </a>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="p-3 bg-white text-center">
                        <h6 class="fw-bold text-truncate mb-0" title="${movie.title}">${movie.title}</h6>
                    </div>
                </div>`;
            return col;
        }

        function renderGrid(movies) {
            if (!movies || movies.length === 0) return;
            list.innerHTML = "";
            const displayMovies = movies.slice(0, 10);
            displayMovies.forEach((movie, index) => {
                const rank = index + 1;
                const card = renderMovieCard(movie, rank);
                list.appendChild(card);
            });
        }

        const popularMovies = (window.HomeIndexConfig && window.HomeIndexConfig.popularMovies) ? window.HomeIndexConfig.popularMovies : [];

        if (popularMovies.length > 0) {
            renderGrid(popularMovies);
        }

        fetch("/Home/Recommend")
            .then(res => res.json())
            .then(data => {
                if (data && data.length > 0) {
                    renderGrid(data);
                }
            })
            .catch(err => console.log(err));
    }

    const theaterSelect = document.getElementById("theaterSelect");
    const movieSelect = document.getElementById("movieSelect");
    const dateSelect = document.getElementById("dateSelect");
    const showtimeSelect = document.getElementById("showtimeSelect");

    if (theaterSelect && movieSelect && dateSelect && showtimeSelect) {
        theaterSelect.addEventListener("change", function () {
            const theaterId = this.value;

            movieSelect.innerHTML = '<option selected disabled>-- Chọn phim --</option>';
            dateSelect.innerHTML = '<option selected disabled>-- Chọn ngày --</option>';
            showtimeSelect.innerHTML = '<option selected disabled>-- Chọn suất chiếu --</option>';

            fetch(`/Home/GetMoviesByTheater?theaterId=${theaterId}`)
                .then(res => res.json())
                .then(data => {
                    data.forEach(m => {
                        const opt = document.createElement("option");
                        opt.value = m.movieId;
                        opt.textContent = m.title;
                        movieSelect.appendChild(opt);
                    });
                });
        });

        movieSelect.addEventListener("change", function () {
            const theaterId = theaterSelect.value;
            const movieId = this.value;

            dateSelect.innerHTML = '<option selected disabled>-- Chọn ngày --</option>';

            fetch(`/Home/GetShowtimes?theaterId=${theaterId}&movieId=${movieId}`)
                .then(res => res.json())
                .then(data => {
                    const dates = [...new Set(data.map(s => s.date))];

                    dates.forEach(d => {
                        const opt = document.createElement("option");
                        opt.value = d;
                        const [y, m, day] = d.split("-");
                        opt.textContent = `${day}/${m}/${y}`;
                        dateSelect.appendChild(opt);
                    });

                    dateSelect.onchange = function () {
                        const selectedDate = this.value;

                        showtimeSelect.innerHTML = '<option selected disabled>-- Chọn suất chiếu --</option>';

                        data.filter(s => s.date === selectedDate)
                            .forEach(s => {
                                const opt = document.createElement("option");
                                opt.value = s.showtimeId;
                                opt.textContent = `${s.time} - ${Number(s.price).toLocaleString("vi-VN")}đ`;
                                showtimeSelect.appendChild(opt);
                            });
                    };
                });
        });
    }
});

window.changeMap = function (address, element) {
    document.querySelectorAll(".theater-compact-item, .theater-card")
        .forEach(x => x.classList.remove("active"));

    if (element)
        element.classList.add("active");

    const map = document.getElementById("theaterMap");
    if (map) {
        map.src = "https://maps.google.com/maps?q=" + encodeURIComponent(address) + "&output=embed";
    }
};

function scrollToMapAndChange(address) {
    const mapSection = document.querySelector(".theater-section") || document.querySelector(".map-container");

    if (mapSection)
        mapSection.scrollIntoView({ behavior: "smooth" });

    document.querySelectorAll(".theater-compact-item, .theater-card").forEach(card => {
        const addrElement = card.querySelector("[title]") || card.querySelector(".text-truncate") || card.querySelector(".theater-info p");
        const cardAddress = addrElement ? (addrElement.getAttribute("title") || addrElement.innerText.trim()) : "";

        card.classList.remove("active");

        if (cardAddress && (cardAddress.includes(address) || address.includes(cardAddress))) {
            card.classList.add("active");
            changeMap(address, card);
        }
    });
}
