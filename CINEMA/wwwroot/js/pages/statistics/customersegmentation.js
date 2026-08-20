document.addEventListener("DOMContentLoaded", function () {
    const config = window.CustomerSegmentationConfig;
    if (!config || !config.clusterProfiles) return;

    const clusterProfiles = config.clusterProfiles || [];
    const customers = config.customers || [];

    const clusterColors = [
        'rgba(13, 110, 253, 0.85)',
        'rgba(25, 135, 84, 0.85)',
        'rgba(255, 193, 7, 0.95)',
        'rgba(220, 53, 69, 0.85)',
        'rgba(13, 202, 240, 0.85)'
    ];

    const clusterBorders = [
        '#0d6efd',
        '#198754',
        '#ffc107',
        '#dc3545',
        '#0dcaf0'
    ];

    const pieCanvas = document.getElementById('pieChart');
    if (pieCanvas && window.Chart) {
        const pieCtx = pieCanvas.getContext('2d');
        new Chart(pieCtx, {
            type: 'doughnut',
            data: {
                labels: clusterProfiles.map(p => p.label),
                datasets: [{
                    data: clusterProfiles.map(p => p.customerCount),
                    backgroundColor: clusterProfiles.map((p, idx) => clusterColors[idx % 5]),
                    borderWidth: 1,
                    borderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            boxWidth: 12,
                            font: {
                                family: 'Be Vietnam Pro',
                                size: 11
                            }
                        }
                    }
                },
                cutout: '65%'
            }
        });
    }

    const scatterCanvas = document.getElementById('scatterChart');
    if (scatterCanvas && window.Chart) {
        const scatterCtx = scatterCanvas.getContext('2d');
        const scatterDatasets = clusterProfiles.map((p, idx) => {
            const clusterPoints = customers
                .filter(c => c.clusterId === p.clusterId)
                .map(c => ({
                    x: c.ticketsPerMonth,
                    y: c.comboSpending
                }));

            return {
                label: p.label,
                data: clusterPoints,
                backgroundColor: clusterColors[idx % 5],
                borderColor: clusterBorders[idx % 5],
                pointRadius: 8,
                pointHoverRadius: 10
            };
        });

        new Chart(scatterCtx, {
            type: 'scatter',
            data: {
                datasets: scatterDatasets
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        callbacks: {
                            label: function(context) {
                                return `${context.dataset.label}: ${context.raw.x.toFixed(1)} vé/tháng, ${context.raw.y.toLocaleString()}đ Combo`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        title: {
                            display: true,
                            text: 'Tần suất mua vé (vé/tháng)',
                            font: { family: 'Be Vietnam Pro', weight: 'bold' }
                        },
                        grid: {
                            color: '#f1f1f1'
                        }
                    },
                    y: {
                        title: {
                            display: true,
                            text: 'Chi tiêu Combo (VNĐ)',
                            font: { family: 'Be Vietnam Pro', weight: 'bold' }
                        },
                        grid: {
                            color: '#f1f1f1'
                        }
                    }
                }
            }
        });
    }

    const profileCanvas = document.getElementById('profileChart');
    if (profileCanvas && window.Chart) {
        const profileCtx = profileCanvas.getContext('2d');
        new Chart(profileCtx, {
            type: 'bar',
            data: {
                labels: clusterProfiles.map(p => p.label),
                datasets: [
                    {
                        label: 'Vé / Tháng',
                        data: clusterProfiles.map(p => p.avgTicketsPerMonth),
                        backgroundColor: 'rgba(54, 162, 235, 0.75)',
                        borderColor: '#36a2eb',
                        borderWidth: 1
                    },
                    {
                        label: 'Chi tiêu Combo (x100k đ)',
                        data: clusterProfiles.map(p => p.avgComboSpending / 100000),
                        backgroundColor: 'rgba(75, 192, 192, 0.75)',
                        borderColor: '#4bc0c0',
                        borderWidth: 1
                    },
                    {
                        label: 'Tỷ lệ Cuối Tuần (x10)',
                        data: clusterProfiles.map(p => p.avgWeekendRatio * 10),
                        backgroundColor: 'rgba(255, 159, 64, 0.75)',
                        borderColor: '#ff9f40',
                        borderWidth: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            boxWidth: 12,
                            font: { family: 'Be Vietnam Pro', size: 10 }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: '#f1f1f1'
                        }
                    }
                }
            }
        });
    }

    const tableSearch = document.getElementById('tableSearch');
    if (tableSearch) {
        tableSearch.addEventListener('keyup', function() {
            const query = this.value.toLowerCase();
            const rows = document.querySelectorAll('#segmentTable tbody tr');
            
            rows.forEach(row => {
                const name = row.cells[2].textContent.toLowerCase();
                const email = row.cells[3].textContent.toLowerCase();
                if (name.includes(query) || email.includes(query)) {
                    row.style.display = '';
                } else {
                    row.style.display = 'none';
                }
            });
        });
    }
});
