let cinemaChart, movieChart, ticketsByDateChart, ticketsByMonthChart, ticketsByYearChart, showtimeChart;

document.addEventListener("DOMContentLoaded", function () {
    const config = window.AdvancedStatsConfig || {};
    const barOptions = {
        barPercentage: 0.4,
        categoryPercentage: 0.5,
        scales: {
            y: {
                beginAtZero: true,
                max: 20,
                ticks: {
                    stepSize: 2
                }
            }
        }
    };

    const cinemaCanvas = document.getElementById("cinemaChart");
    if (cinemaCanvas && window.Chart) {
        cinemaChart = new Chart(cinemaCanvas, {
            type: 'bar',
            data: {
                labels: config.cinemaLabels || [],
                datasets: [{ label: 'Số vé', data: config.cinemaRevenue || [], backgroundColor: '#3b82f6' }]
            },
            options: barOptions
        });
    }

    const dateCanvas = document.getElementById("ticketsByDateChart");
    if (dateCanvas && window.Chart) {
        ticketsByDateChart = new Chart(dateCanvas, {
            type: 'bar',
            data: { labels: config.labelsByDate || [], datasets: [{ label: 'Số vé', data: config.ticketCountByDate || [], backgroundColor: '#10b981' }] },
            options: barOptions
        });
    }

    const monthCanvas = document.getElementById("ticketsByMonthChart");
    if (monthCanvas && window.Chart) {
        ticketsByMonthChart = new Chart(monthCanvas, {
            type: 'bar',
            data: { labels: config.labelsByMonth || [], datasets: [{ label: 'Số vé', data: config.ticketCountByMonth || [], backgroundColor: '#8b5cf6' }] },
            options: barOptions
        });
    }

    const yearCanvas = document.getElementById("ticketsByYearChart");
    if (yearCanvas && window.Chart) {
        ticketsByYearChart = new Chart(yearCanvas, {
            type: 'bar',
            data: { labels: config.labelsByYear || [], datasets: [{ label: 'Số vé', data: config.ticketCountByYear || [], backgroundColor: '#f59e0b' }] },
            options: barOptions
        });
    }

    loadMovieData();
    loadShowtimeChart();
});

async function loadTheaterData() {
    const select = document.querySelector('select[name="tId"]');
    if (!select) return;
    const tId = select.value;
    const response = await fetch('/Statistics/GetTheaterData?tId=' + tId);
    const data = await response.json();
    if (cinemaChart) {
        cinemaChart.data.labels = data.labels;
        cinemaChart.data.datasets[0].data = data.values;
        cinemaChart.update();
    }
}

async function loadMovieData() {
    const movieSelect = document.getElementById("movieSelect");
    if (!movieSelect) return;
    const mId = movieSelect.value;

    const response = await fetch(`/Statistics/GetMovieTicketData?mId=${mId}`);
    const data = await response.json();

    if (movieChart) {
        movieChart.destroy();
    }

    const movieCanvas = document.getElementById("movieChart");
    if (movieCanvas && window.Chart) {
        const ctx = movieCanvas.getContext('2d');
        movieChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: data.labels,
                datasets: [{
                    label: 'Số vé bán ra',
                    data: data.values,
                    backgroundColor: '#6610f2'
                }]
            },
            options: {
                barPercentage: 0.4,
                categoryPercentage: 0.5,
                scales: {
                    y: {
                        beginAtZero: true,
                        max: 20,
                        ticks: {
                            stepSize: 2,
                            precision: 0
                        }
                    }
                }
            }
        });
    }
}

async function loadShowtimeChart() {
    const mSelect = document.getElementById("showtimeMovieSelect");
    const tSelect = document.getElementById("showtimeTheaterSelect");
    if (!mSelect || !tSelect) return;
    const mId = mSelect.value;
    const tId2 = tSelect.value;

    const res = await fetch(`/Statistics/GetShowtimeTicketData?mId=${mId}&tId2=${tId2}`);
    const data = await res.json();

    if (showtimeChart) showtimeChart.destroy();

    const stCanvas = document.getElementById("showtimeChart");
    if (stCanvas && window.Chart) {
        const ctx = stCanvas.getContext('2d');
        showtimeChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: data.labels,
                datasets: [{
                    label: 'Số vé bán ra',
                    data: data.values,
                    borderColor: '#10b981',
                    backgroundColor: 'rgba(16, 185, 129, 0.2)',
                    fill: true,
                    tension: 0.3
                }]
            },
            options: {
                scales: {
                    y: {
                        beginAtZero: true,
                        min: 0,
                        max: 20,
                        ticks: {
                            stepSize: 2
                        }
                    }
                }
            }
        });
    }
}
