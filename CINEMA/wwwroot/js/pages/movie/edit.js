function previewImageFromFile(input) {
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = function(e) {
            const posterPreview = document.getElementById('poster-preview');
            if (posterPreview) posterPreview.src = e.target.result;
        };
        reader.readAsDataURL(input.files[0]);
    }
}

function previewImageFromUrl(url) {
    const posterPreview = document.getElementById('poster-preview');
    if (!posterPreview) return;
    const trimmedUrl = url.trim();
    if (trimmedUrl) {
        posterPreview.src = trimmedUrl;
    } else {
        posterPreview.src = "https://placehold.co/300x450?text=No+Poster";
    }
}

function previewTrailer(url) {
    const trimmedUrl = url.trim();
    const iframe = document.getElementById('trailer-iframe');
    const wrapper = document.getElementById('trailer-video-wrapper');
    const noPreview = document.getElementById('trailer-no-preview');

    if (!iframe || !wrapper || !noPreview) return;

    if (!trimmedUrl) {
        wrapper.classList.add('d-none');
        noPreview.classList.remove('d-none');
        iframe.src = "";
        return;
    }

    let embedUrl = "";
    if (trimmedUrl.includes("youtube.com") || trimmedUrl.includes("youtu.be")) {
        let videoId = "";
        if (trimmedUrl.includes("v=")) {
            videoId = trimmedUrl.split("v=")[1].split("&")[0];
        } else if (trimmedUrl.includes("youtu.be/")) {
            videoId = trimmedUrl.split("youtu.be/")[1].split("?")[0];
        }
        if (videoId) {
            embedUrl = "https://www.youtube.com/embed/" + videoId;
        }
    } else {
        embedUrl = trimmedUrl;
    }

    if (embedUrl) {
        iframe.src = embedUrl;
        wrapper.classList.remove('d-none');
        noPreview.classList.add('d-none');
    } else {
        wrapper.classList.add('d-none');
        noPreview.classList.remove('d-none');
        iframe.src = "";
    }
}

// 💡 Tự động chuyển sang TẠM NGỪNG CHIẾU khi chọn Ngày kết thúc đã qua (< hôm nay)
document.addEventListener('DOMContentLoaded', function () {
    const endDateInput = document.querySelector('input[name="EndDate"]');
    const statusActive = document.getElementById('statusActive');
    const statusInactive = document.getElementById('statusInactive');

    if (!endDateInput) return;

    // Thêm thẻ thông báo ngay bên dưới ô nhập Ngày kết thúc
    const warningBadge = document.createElement('small');
    warningBadge.id = 'endDateAutoStopNotice';
    warningBadge.className = 'text-danger fw-bold d-none mt-1.5 d-block';
    warningBadge.innerHTML = '<i class="bi bi-exclamation-triangle-fill me-1"></i> Ngày kết thúc chiếu đã qua -> Hệ thống tự động chuyển sang <strong>TẠM NGỪNG CHIẾU</strong>';
    endDateInput.parentNode.parentNode.appendChild(warningBadge);

    function checkAndAutoStopStatus() {
        if (!endDateInput.value) {
            warningBadge.classList.add('d-none');
            return;
        }

        const today = new Date();
        today.setHours(0, 0, 0, 0);

        const selectedDate = new Date(endDateInput.value);
        selectedDate.setHours(0, 0, 0, 0);

        if (selectedDate < today) {
            if (statusInactive) {
                statusInactive.checked = true;
            }
            warningBadge.classList.remove('d-none');
        } else {
            warningBadge.classList.add('d-none');
        }
    }

    endDateInput.addEventListener('change', checkAndAutoStopStatus);
    endDateInput.addEventListener('input', checkAndAutoStopStatus);
    checkAndAutoStopStatus();
});

