document.addEventListener("DOMContentLoaded", function () {
    const movieSelect = document.getElementById("movieSelect");
    const startTimeInput = document.getElementById("startTimeInput");
    const endTimeInput = document.getElementById("endTimeInput");

    if (movieSelect && startTimeInput && endTimeInput) {
        movieSelect.addEventListener("change", autoCalculateEndTime);
        startTimeInput.addEventListener("change", autoCalculateEndTime);

        function autoCalculateEndTime() {
            let selectedOption = movieSelect.options[movieSelect.selectedIndex];
            if (!selectedOption) return;
            let durationStr = selectedOption.getAttribute("data-duration");
            let startTimeVal = startTimeInput.value;

            if (durationStr && startTimeVal) {
                let durationMinutes = parseInt(durationStr);
                let startTime = new Date(startTimeVal);

                startTime.setMinutes(startTime.getMinutes() + durationMinutes);

                let year = startTime.getFullYear();
                let month = String(startTime.getMonth() + 1).padStart(2, '0');
                let day = String(startTime.getDate()).padStart(2, '0');
                let hours = String(startTime.getHours()).padStart(2, '0');
                let minutes = String(startTime.getMinutes()).padStart(2, '0');

                endTimeInput.value = `${year}-${month}-${day}T${hours}:${minutes}`;
            }
        }
    }
});
