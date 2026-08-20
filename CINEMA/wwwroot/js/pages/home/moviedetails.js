window.copyShareLink = function(button) {
    const url = window.location.href;
    const showSuccess = () => {
        const originalContent = button.innerHTML;
        button.innerHTML = '<i class="bi bi-check2"></i> Đã sao chép!';
        button.classList.add('btn-primary', 'text-white');
        setTimeout(() => {
            button.innerHTML = originalContent;
            button.classList.remove('btn-primary', 'text-white');
        }, 2000);
    };

    if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(url).then(showSuccess).catch(() => fallbackCopyText(url, showSuccess));
    } else {
        fallbackCopyText(url, showSuccess);
    }
};

function fallbackCopyText(text, callback) {
    const textArea = document.createElement("textarea");
    textArea.value = text;
    textArea.style.position = "fixed";
    textArea.style.opacity = "0";
    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();
    try {
        if (document.execCommand('copy')) callback();
    } catch (err) {
        alert('Không thể sao chép tự động. Hãy copy liên kết trên thanh địa chỉ nhé!');
    }
    document.body.removeChild(textArea);
}

function setRating(val) {
    const ratingInput = document.getElementById("revRating");
    if (ratingInput) ratingInput.value = val;
    document.querySelectorAll(".star-select").forEach(star => {
        let starVal = parseInt(star.dataset.val);
        if (starVal <= val) {
            star.classList.remove("bi-star");
            star.classList.add("bi-star-fill");
        } else {
            star.classList.remove("bi-star-fill");
            star.classList.add("bi-star");
        }
    });
}

async function submitReview() {
    const movieIdEl = document.getElementById("revMovieId");
    const orderIdEl = document.getElementById("revOrderId");
    const ratingEl = document.getElementById("revRating");
    const commentEl = document.getElementById("revComment");

    if (!commentEl) return;
    const movieId = movieIdEl ? movieIdEl.value : '';
    const orderId = orderIdEl ? orderIdEl.value : '';
    const rating = ratingEl ? ratingEl.value : '';
    const comment = commentEl.value.trim();

    if (!comment) return;

    const btn = document.getElementById("btnSubmitReview");
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Đang gửi...';
    }

    try {
        const formData = new FormData();
        formData.append("movieId", movieId);
        formData.append("rating", rating);
        formData.append("comment", comment);
        if (orderId) formData.append("orderId", orderId);

        const r = await fetch('/Home/AddReview', {
            method: 'POST',
            body: formData
        });
        const res = await r.json();

        if (res.success) {
            alert(res.message);
            location.reload();
        } else {
            alert(res.message);
            if (btn) {
                btn.disabled = false;
                btn.innerHTML = '<i class="bi bi-send-fill me-1"></i> Gửi đánh giá';
            }
        }
    } catch(e) {
        alert('Có lỗi xảy ra khi gửi đánh giá.');
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-send-fill me-1"></i> Gửi đánh giá';
        }
    }
}

async function likeReview(reviewId, btn) {
    try {
        const formData = new FormData();
        formData.append("reviewId", reviewId);

        const r = await fetch('/Home/LikeReview', {
            method: 'POST',
            body: formData
        });
        const res = await r.json();

        if (res.success) {
            const span = btn.querySelector('.like-cnt');
            if (span) span.innerText = res.likesCount;
            btn.classList.remove('btn-outline-secondary');
            btn.classList.add('btn-primary', 'text-white');
            btn.disabled = true;
        } else {
            alert(res.message);
        }
    } catch(e) {
        alert('Lỗi kết nối.');
    }
}

async function reportReview(reviewId, reason) {
    if (!confirm(`Bạn muốn báo cáo bình luận này vì lý do: "${reason}"?`)) return;

    try {
        const formData = new FormData();
        formData.append("reviewId", reviewId);
        formData.append("reason", reason);

        const r = await fetch('/Home/ReportReview', {
            method: 'POST',
            body: formData
        });

        if (!r.ok) {
            alert(`Lỗi từ Server (Mã lỗi: ${r.status}). Vui lòng kiểm tra lại hàm ReportReview trong Controller!`);
            return;
        }

        const res = await r.json();

        if (res.success) {
            alert(res.message);
        } else {
            alert(res.message);
        }
    } catch(e) {
        console.error(e);
        alert('Lỗi kết nối mạng hoặc không thể đọc dữ liệu.');
    }
}
