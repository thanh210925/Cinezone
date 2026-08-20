document.addEventListener("DOMContentLoaded", function () {
    const quickMonth = document.getElementById('quickMonthFilter');
    if (quickMonth) {
        quickMonth.addEventListener('change', function() {
            const selectedMonthValue = this.value;
            if (!selectedMonthValue) return;

            const [year, month] = selectedMonthValue.split('-').map(Number);
            const fromDate = `${year}-${String(month).padStart(2, '0')}-01`;
            const lastDay = new Date(year, month, 0).getDate();
            const toDate = `${year}-${String(month).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}`;

            const inputFrom = document.getElementById('inputFrom');
            const inputTo = document.getElementById('inputTo');
            const filterForm = document.getElementById('filterForm');
            if (inputFrom) inputFrom.value = fromDate;
            if (inputTo) inputTo.value = toDate;
            if (filterForm) filterForm.submit();
        });
    }

    const inputFrom = document.getElementById('inputFrom');
    const inputTo = document.getElementById('inputTo');
    if (inputFrom && inputTo) {
        const fromVal = inputFrom.value;
        const toVal = inputTo.value;

        if (fromVal && toVal) {
            const fromPrefix = fromVal.substring(0, 7);
            const toPrefix = toVal.substring(0, 7);

            if (fromPrefix === toPrefix && fromVal.endsWith('-01')) {
                const selectElement = document.getElementById('quickMonthFilter');
                if (selectElement) {
                    const option = selectElement.querySelector(`option[value="${fromPrefix}"]`);
                    if (option) {
                        selectElement.value = fromPrefix;
                    }
                }
            }
        }
    }

    const selectFilters = document.querySelectorAll('#filterForm select[name="theaterId"], #filterForm select[name="movieId"]');
    selectFilters.forEach(sel => {
        sel.addEventListener('change', () => {
            const filterForm = document.getElementById('filterForm');
            if (filterForm) filterForm.submit();
        });
    });

    initCharts();
});

function createMixedChart(id, labels, data1, data2) {
    const el = document.getElementById(id);
    if (!el || !window.Chart) return;
    new Chart(el, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [
                { 
                    label: 'Doanh thu (₫)', 
                    data: data1, 
                    yAxisID: 'y', 
                    backgroundColor: 'rgba(13, 110, 253, 0.85)', 
                    borderRadius: 6, 
                    maxBarThickness: 45 
                },
                { 
                    label: 'Số vé bán', 
                    data: data2, 
                    type: 'line', 
                    yAxisID: 'y1', 
                    borderColor: '#ff8c00', 
                    backgroundColor: 'rgba(255, 140, 0, 0.1)',
                    borderWidth: 3, 
                    tension: 0.3,
                    fill: false
                }
            ]
        },
        options: { 
            responsive: true, 
            maintainAspectRatio: false,
            plugins: {
                legend: { position: 'top', labels: { boxWidth: 15, font: { weight: 600 } } }
            },
            scales: {
                y: { position: 'left', grid: { drawOnChartArea: true } },
                y1: { position: 'right', grid: { drawOnChartArea: false } }
            }
        }
    });
}

function initCharts() {
    if (!window.Chart) return;
    const cfg = window.StatisticsIndexConfig || {};

    createMixedChart("revenueByDateChart", cfg.labelsByDate || [], cfg.revenueByDate || [], cfg.ticketCountByDate || []);
    createMixedChart("revenueByMonthChart", cfg.labelsByMonth || [], cfg.revenueByMonth || [], cfg.ticketCountByMonth || []);
    createMixedChart("revenueByYearChart", cfg.labelsByYear || [], cfg.revenueByYear || [], cfg.ticketCountByYear || []);

    const pmEl = document.getElementById("paymentMethodChart");
    if (pmEl) {
        new Chart(pmEl, {
            type: 'doughnut',
            data: {
                labels: cfg.paymentMethodLabels || [],
                datasets: [{
                    data: cfg.paymentMethodValues || [],
                    backgroundColor: ['#20c997', '#0d6efd', '#fd7e14', '#6c757d'],
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { position: 'right', labels: { boxWidth: 12 } }
                },
                cutout: '65%'
            }
        });
    }

    const mrEl = document.getElementById("movieRevenueChart");
    if (mrEl) {
        new Chart(mrEl, {
            type: 'bar',
            data: {
                labels: cfg.movieLabels || [],
                datasets: [{ 
                    label: 'Doanh thu phim (₫)', 
                    data: cfg.revenueByMovie || [], 
                    backgroundColor: 'rgba(25, 135, 84, 0.85)', 
                    borderRadius: 6, 
                    maxBarThickness: 45 
                }]
            },
            options: { 
                responsive: true, 
                maintainAspectRatio: false,
                plugins: { legend: { display: false } }
            }
        });
    }

    const tcEl = document.getElementById("ticketComparisonChart");
    if (tcEl) {
        new Chart(tcEl, {
            type: 'doughnut',
            data: {
                labels: cfg.movieLabels || [],
                datasets: [{ 
                    data: cfg.ticketData || [], 
                    backgroundColor: ['#0dcaf0', '#6f42c1', '#fd7e14', '#20c997', '#ffc107'],
                    hoverOffset: 6 
                }]
            },
            options: { 
                responsive: true, 
                maintainAspectRatio: false, 
                plugins: { legend: { position: 'bottom' } } 
            }
        });
    }

    const aovEl = document.getElementById("aovChart");
    if (aovEl) {
        new Chart(aovEl, {
            type: 'bar',
            data: {
                labels: ['Hôm nay', 'Tháng này', 'Quý này', 'Năm nay'],
                datasets: [{
                    label: 'AOV (VND)',
                    data: [cfg.avgOrderValueToday || 0, cfg.avgOrderValueMonth || 0, cfg.avgOrderValueQuarter || 0, cfg.avgOrderValueYear || 0],
                    backgroundColor: ['#f06292', '#4fc3f7', '#fff176', '#4db6ac'],
                    borderRadius: 6,
                    maxBarThickness: 45
                }]
            },
            options: { responsive: true, maintainAspectRatio: false }
        });
    }

    const mcEl = document.getElementById("monthCompareChart");
    if (mcEl) {
        new Chart(mcEl, {
            type: 'bar',
            data: {
                labels: ['Tháng trước', 'Tháng này'],
                datasets: [{
                    label: 'Doanh thu (VND)',
                    data: [cfg.revenueLastMonth || 0, cfg.revenueCurrentMonth || 0],
                    backgroundColor: ['#adb5bd', '#198754'],
                    borderRadius: 6,
                    maxBarThickness: 50
                }]
            },
            options: { responsive: true, maintainAspectRatio: false }
        });
    }

    const srEl = document.getElementById("showtimeRevenueChart");
    if (srEl) {
        new Chart(srEl, {
            type: 'bar',
            data: {
                labels: cfg.showtimeLabels || [],
                datasets: [{ 
                    label: 'Doanh thu vé (₫)', 
                    data: cfg.revenueByShowtime || [], 
                    backgroundColor: 'rgba(111, 66, 193, 0.85)', 
                    borderRadius: 6, 
                    maxBarThickness: 45 
                }]
            },
            options: { responsive: true, maintainAspectRatio: false }
        });
    }

    const thcEl = document.getElementById("theaterComparisonChart");
    if (thcEl) {
        new Chart(thcEl, {
            type: 'bar',
            data: {
                labels: cfg.theaterLabels || [],
                datasets: [{ 
                    label: 'Tổng doanh thu (₫)', 
                    data: cfg.theaterRevenueData || [], 
                    backgroundColor: 'rgba(239, 68, 68, 0.85)', 
                    borderRadius: 6, 
                    maxBarThickness: 45 
                }]
            },
            options: { responsive: true, maintainAspectRatio: false }
        });
    }

    const phEl = document.getElementById("peakHoursChart");
    if (phEl) {
        new Chart(phEl, {
            type: 'bar',
            data: {
                labels: cfg.peakHoursLabels || [],
                datasets: [
                    { 
                        label: 'Số lượng vé đặt', 
                        data: cfg.peakHoursTickets || [], 
                        yAxisID: 'y', 
                        backgroundColor: '#0dcaf0', 
                        borderRadius: 4 
                    },
                    { 
                        label: 'Doanh thu (₫)', 
                        data: cfg.peakHoursRevenue || [], 
                        type: 'line', 
                        yAxisID: 'y1', 
                        borderColor: '#6f42c1', 
                        borderWidth: 2, 
                        tension: 0.3,
                        fill: false
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: { position: 'left' },
                    y1: { position: 'right', grid: { drawOnChartArea: false } }
                }
            }
        });
    }

    const pdEl = document.getElementById("peakDaysChart");
    if (pdEl) {
        new Chart(pdEl, {
            type: 'line',
            data: {
                labels: cfg.peakDaysLabels || [],
                datasets: [
                    { 
                        label: 'Doanh thu bán vé (₫)', 
                        data: cfg.peakDaysRevenue || [], 
                        borderColor: '#198754', 
                        backgroundColor: 'rgba(25, 135, 84, 0.05)',
                        borderWidth: 3, 
                        tension: 0.3,
                        fill: true
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false
            }
        });
    }

    const tcComboEl = document.getElementById('topComboChart');
    if (tcComboEl) {
        new Chart(tcComboEl, {
            type: 'bar',
            data: {
                labels: cfg.topComboLabels || [],
                datasets: [{ 
                    label: 'Số lượng bán', 
                    data: cfg.topComboValues || [], 
                    backgroundColor: '#0d6efd', 
                    borderRadius: 6, 
                    maxBarThickness: 45 
                }]
            },
            options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } } }
        });
    }

    const trComboEl = document.getElementById('topRevenueComboChart');
    if (trComboEl) {
        new Chart(trComboEl, {
            type: 'bar',
            data: {
                labels: cfg.topRevenueComboLabels || [],
                datasets: [{ 
                    label: 'Doanh thu (₫)', 
                    data: cfg.topRevenueComboValues || [], 
                    backgroundColor: '#56ab2f', 
                    borderRadius: 6, 
                    maxBarThickness: 45 
                }]
            },
            options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } } }
        });
    }

    const cpEl = document.getElementById("comboPieChart");
    if (cpEl) {
        new Chart(cpEl, {
            type: 'pie',
            data: {
                labels: cfg.comboPieLabels || [],
                datasets: [{ data: cfg.comboPieValues || [], backgroundColor: ['#0d6efd', '#20c997', '#fd7e14', '#e91e63', '#adb5bd'], hoverOffset: 4 }]
            },
            options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } } }
        });
    }

    const voucherCtx = document.getElementById('voucherChart');
    if (voucherCtx) {
        new Chart(voucherCtx, {
            type: 'bar',
            data: {
                labels: cfg.voucherLabels || [],
                datasets: [{ 
                    label: 'Lượt sử dụng', 
                    data: cfg.voucherValues || [], 
                    backgroundColor: '#e91e63', 
                    borderRadius: 6, 
                    maxBarThickness: 45 
                }]
            },
            options: { responsive: true, maintainAspectRatio: false }
        });
    }
}

function exportToCSV() {
    const cfg = window.StatisticsIndexConfig || {};
    let csvContent = "\uFEFF";
    
    const inputFrom = document.getElementById('inputFrom');
    const inputTo = document.getElementById('inputTo');
    const fromVal = inputFrom ? inputFrom.value : '';
    const toVal = inputTo ? inputTo.value : '';

    csvContent += "BÁO CÁO THỐNG KÊ DOANH THU CINEZONE\r\n";
    csvContent += "Thời gian:," + (fromVal || "Toàn thời gian") + " - " + (toVal || "Toàn thời gian") + "\r\n\r\n";

    csvContent += "1. TỔNG QUAN CHỈ SỐ TÀI CHÍNH\r\n";
    csvContent += "Chỉ số,Giá trị\r\n";
    csvContent += "Doanh thu thuần (Thực nhận)," + (cfg.netRevenue || 0) + " ₫\r\n";
    csvContent += "Doanh thu bán Vé gộp," + (cfg.ticketRevenue || 0) + " ₫\r\n";
    csvContent += "Doanh thu bán Combo gộp," + (cfg.grossComboRevenue || 0) + " ₫\r\n";
    csvContent += "Khấu hao giảm giá Voucher," + (cfg.totalDiscount || 0) + " ₫\r\n";
    csvContent += "Tổng số vé bán ra," + (cfg.totalTickets || 0) + "\r\n";
    csvContent += "Tổng số combo bán ra," + (cfg.comboSold || 0) + "\r\n\r\n";

    csvContent += "2. HIỆU SUẤT THEO RẠP CHIẾU\r\n";
    csvContent += "Tên rạp,Số vé đã bán,Doanh thu vé,Doanh thu Combo,Tổng doanh số,Tỷ lệ lấp đầy\r\n";
    (cfg.theaterRevenuesList || []).forEach(r => {
        csvContent += `"${r.theaterName}",${r.ticketsSold},${r.ticketRevenue},${r.comboRevenue},${r.totalRevenue},${r.occupancyRate}%\r\n`;
    });
    csvContent += "\r\n";

    csvContent += "3. DOANH THU THEO PHIM\r\n";
    csvContent += "Tên phim,Doanh thu gộp,Số vé bán ra\r\n";
    const mLabels = cfg.movieLabels || [];
    const rByMovie = cfg.revenueByMovie || [];
    const tData = cfg.ticketData || [];
    for (let i = 0; i < mLabels.length; i++) {
        csvContent += `"${mLabels[i]}",${rByMovie[i] || 0},${tData[i] || 0}\r\n`;
    }
    csvContent += "\r\n";

    csvContent += "4. TOP KHÁCH HÀNG THÂN THIẾT\r\n";
    csvContent += "Họ tên,Email,Số điện thoại,Hạng thành viên,Số vé đã đặt,Tổng chi tiêu thực nhận\r\n";
    (cfg.topCustomersList || []).forEach(c => {
        csvContent += `"${c.fullName}","${c.email}","${c.phone}","${c.membershipLevel}",${c.ticketsBooked},${c.totalSpent}\r\n`;
    });
    csvContent += "\r\n";

    let blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    let link = document.createElement("a");
    let url = URL.createObjectURL(blob);
    link.setAttribute("href", url);
    link.setAttribute("download", "CineZone_BaoCaoDoanhThu_" + new Date().toISOString().slice(0, 10) + ".csv");
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}
