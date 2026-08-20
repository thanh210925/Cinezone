document.addEventListener("DOMContentLoaded", function () {
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    if (window.bootstrap && window.bootstrap.Tooltip) {
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }
});

function filterTable() {
    let input = document.getElementById("searchInput");
    if (!input) return;
    let filter = input.value.toLowerCase();
    let table = document.querySelector(".custom-table");
    if (!table) return;
    let rows = table.getElementsByTagName("tr");

    for (let i = 1; i < rows.length; i++) {
        let cols = rows[i].getElementsByTagName("td");
        if (cols.length > 2) {
            let genreName = cols[1].textContent.toLowerCase();
            let desc = cols[2].textContent.toLowerCase();

            if (genreName.indexOf(filter) > -1 || desc.indexOf(filter) > -1) {
                rows[i].style.display = "";
            } else {
                rows[i].style.display = "none";
            }
        }
    }
}
