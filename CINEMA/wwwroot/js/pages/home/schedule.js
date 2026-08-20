document.addEventListener("DOMContentLoaded", function() {
    let activeTab = document.querySelector(".date-tab-active");
    if (activeTab) {
        activeTab.scrollIntoView({ behavior: 'auto', block: 'nearest', inline: 'center' });
    }
});

function filterTime(range, btn) {
    document.querySelectorAll('.time-filter-btn').forEach(b => {
        b.classList.remove('btn-pink-active');
        b.classList.add('btn-light-grey');
    });
    if (btn) {
        btn.classList.remove('btn-light-grey');
        btn.classList.add('btn-pink-active');
    }

    let minHour = 0, maxHour = 24;
    if (range === 'truoc12') { maxHour = 12; }
    else if (range === '12to15') { minHour = 12; maxHour = 15; }
    else if (range === '15to18') { minHour = 15; maxHour = 18; }
    else if (range === '18to21') { minHour = 18; maxHour = 21; }
    else if (range === 'sau21') { minHour = 21; }

    let totalVisibleMovies = 0;

    document.querySelectorAll('.movie-card-item').forEach(movieCard => {
        let movieHasVisibleShowtime = false;

        movieCard.querySelectorAll('.theater-block-item').forEach(theaterBlock => {
            let theaterHasVisibleShowtime = false;

            theaterBlock.querySelectorAll('.btn-showtime').forEach(showtimeBtn => {
                let timeStr = showtimeBtn.getAttribute('data-time');
                if (timeStr) {
                    let hour = parseInt(timeStr.split(':')[0]);
                    if (hour >= minHour && hour < maxHour) {
                        showtimeBtn.style.setProperty('display', 'inline-flex', 'important');
                        theaterHasVisibleShowtime = true;
                        movieHasVisibleShowtime = true;
                    } else {
                        showtimeBtn.style.setProperty('display', 'none', 'important');
                    }
                }
            });

            if (theaterHasVisibleShowtime) {
                theaterBlock.style.display = 'block';
            } else {
                theaterBlock.style.display = 'none';
            }
        });

        if (movieHasVisibleShowtime) {
            movieCard.style.display = 'block';
            totalVisibleMovies++;
        } else {
            movieCard.style.display = 'none';
        }
    });

    let noResultMsg = document.getElementById('noShowtimeResultMsg');
    let container = document.getElementById('movieListContainer');
    if (totalVisibleMovies === 0) {
        if (!noResultMsg && container) {
            let msgHtml = `
                <div id="noShowtimeResultMsg" class="text-center py-5">
                    <i class="bi bi-clock-history display-1 text-muted mb-4"></i>
                    <h4 class="text-muted mb-2">Không có suất chiếu phù hợp</h4>
                    <p class="text-muted">Không có suất chiếu nào trong khoảng thời gian đã chọn.</p>
                </div>
            `;
            container.insertAdjacentHTML('afterend', msgHtml);
        } else if (noResultMsg) {
            noResultMsg.style.display = 'block';
        }
    } else {
        if (noResultMsg) {
            noResultMsg.style.display = 'none';
        }
    }
}

function scrollTabs(direction) {
    let container = document.getElementById('dateTabsContainer');
    if (container) {
        let scrollAmount = 270;
        if (direction === 'left') {
            container.scrollBy({ left: -scrollAmount, behavior: 'smooth' });
        } else {
            container.scrollBy({ left: scrollAmount, behavior: 'smooth' });
        }
    }
}
