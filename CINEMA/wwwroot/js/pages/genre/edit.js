document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll('.movie-item').forEach(item => {
        item.addEventListener('click', function (e) {
            if (e.target.tagName !== "INPUT") {
                const checkbox = this.querySelector('input');
                if (checkbox) checkbox.checked = !checkbox.checked;
            }
        });
    });
});
