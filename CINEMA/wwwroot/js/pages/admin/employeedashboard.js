document.addEventListener("DOMContentLoaded", function () {
    const config = window.EmployeeDashboardConfig || {};

    const branchCtx = document.getElementById('branchChart');
    if (branchCtx && config.branchLabels && config.branchValues) {
        new Chart(branchCtx, {
            type: 'bar',
            data: {
                labels: config.branchLabels,
                datasets: [{
                    label: 'Nhân viên',
                    data: config.branchValues
                }]
            }
        });
    }

    const posCtx = document.getElementById('positionChart');
    if (posCtx && config.positionLabels && config.positionValues) {
        new Chart(posCtx, {
            type: 'pie',
            data: {
                labels: config.positionLabels,
                datasets: [{
                    data: config.positionValues
                }]
            }
        });
    }
});
