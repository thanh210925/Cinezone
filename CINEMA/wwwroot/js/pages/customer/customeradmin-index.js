document.addEventListener("DOMContentLoaded", function () {
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    if (window.bootstrap && window.bootstrap.Tooltip) {
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }
});

function searchCustomers() {
    const inputEl = document.getElementById('searchInput');
    if (!inputEl) return;
    const input = inputEl.value.toLowerCase();
    const rows = document.querySelectorAll('.customer-row');
    let count = 0;

    rows.forEach(row => {
        const text = row.innerText.toLowerCase();
        if (text.includes(input)) {
            row.style.display = '';
            count++;
        } else {
            row.style.display = 'none';
        }
    });

    const badge = document.getElementById('totalCustomersBadge');
    if (badge) badge.innerHTML = `Tổng: ${count}`;
}
