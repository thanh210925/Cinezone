function selectPaymentBox(selectedLabel) {
    document.querySelectorAll('.payment-method-box').forEach(box => {
        box.classList.remove('active');
    });
    if (selectedLabel) selectedLabel.classList.add('active');
}

function updatePaymentUI() {
    const checkedInput = document.querySelector('input[name="method"]:checked');
    if (!checkedInput) return;
    const method = checkedInput.value;
    const gatewayInfo = document.getElementById('gateway-info-text');
    const submitBtn = document.getElementById('btn-submit-payment');

    if (!gatewayInfo || !submitBtn) return;

    if (method === 'VietQR') {
        gatewayInfo.innerHTML = 'Quét mã VietQR bằng bất kỳ ứng dụng ngân hàng nào (MBBank, Vietcombank, Techcombank, MoMo...). Hệ thống sẽ <strong>tự động xác nhận thanh toán tức thì</strong>.';
        submitBtn.innerHTML = '<i class="bi bi-qr-code me-2"></i>Thanh toán qua VietQR';
        submitBtn.style.backgroundColor = '#0d6efd';
    }
    else if (method === 'Chuyển khoản') {
        gatewayInfo.innerHTML = 'Bạn sẽ được chuyển hướng an toàn sang cổng thanh toán trực tuyến <strong>VNPAY</strong> để xác thực tài khoản.';
        submitBtn.innerHTML = '<i class="bi bi-qr-code-scan me-2"></i>Thanh toán VNPAY';
        submitBtn.style.backgroundColor = 'var(--color-crimson)';
    } 
    else if (method === 'Online') {
        gatewayInfo.innerHTML = 'Bạn sẽ được chuyển hướng sang cổng thanh toán <strong>Stripe</strong>. Hỗ trợ thẻ tín dụng và ghi nợ quốc tế an toàn.';
        submitBtn.innerHTML = '<i class="bi bi-credit-card-fill me-2"></i>Thanh toán Stripe';
        submitBtn.style.backgroundColor = '#635BFF';
    } 
    else {
        gatewayInfo.innerHTML = 'Bạn đã chọn giữ chỗ. Vui lòng đến rạp trước <strong>15 phút</strong> so với giờ chiếu để thanh toán tiền mặt hoặc quẹt thẻ.';
        submitBtn.innerHTML = '<i class="bi bi-shop me-2"></i>Xác nhận giữ vé';
        submitBtn.style.backgroundColor = '#198754';
    }
}

async function applyCheckoutVoucher() {
    const codeInput = document.getElementById("checkoutVoucherCode");
    if (!codeInput) return;
    const code = codeInput.value.trim();
    const message = document.getElementById("checkoutVoucherMessage");
    if (!message) return;

    if (!code) {
        message.innerText = "❌ Vui lòng nhập mã voucher";
        message.className = "mt-2 small fw-semibold text-danger";
        return;
    }

    message.innerText = "⏳ Đang kiểm tra...";
    message.className = "mt-2 small fw-semibold text-warning";

    const config = window.PaymentIndexConfig || {};
    const originalPrice = parseFloat(config.originalPrice || 0);
    const membershipDiscount = parseFloat(config.membershipDiscountAmount || 0);
    const priceAfterMembership = originalPrice - membershipDiscount;
    const comboTotal = parseFloat(config.comboTotal || 0);

    try {
        let res = await fetch(`/Home/CheckVoucher?code=${encodeURIComponent(code)}&total=${priceAfterMembership}&comboTotal=${comboTotal}`);
        let data = await res.json();

        if (!data.success) {
            message.innerText = "❌ " + data.message;
            message.className = "mt-2 small fw-semibold text-danger";
            return;
        }

        const hiddenCode = document.getElementById("VoucherCode");
        if (hiddenCode) hiddenCode.value = code;
        
        const finalTotal = priceAfterMembership - data.discount;
        const hiddenTotal = document.getElementById("TotalPrice");
        if (hiddenTotal) hiddenTotal.value = finalTotal < 0 ? 0 : finalTotal;

        const rowDiscount = document.getElementById("row-voucher-discount");
        if (rowDiscount) rowDiscount.style.display = "flex";
        const dispCode = document.getElementById("disp-voucher-code");
        if (dispCode) dispCode.innerText = code;
        const dispDiscount = document.getElementById("disp-voucher-discount");
        if (dispDiscount) dispDiscount.innerText = "-" + data.discount.toLocaleString("vi-VN") + " ₫";
        const dispTotal = document.getElementById("disp-total-price");
        if (dispTotal) dispTotal.innerText = (finalTotal < 0 ? 0 : finalTotal).toLocaleString("vi-VN") + " ₫";

        let msgHtml = "✅ Áp dụng voucher thành công! Được giảm " + data.discount.toLocaleString("vi-VN") + "đ";
        if (data.terms) {
            msgHtml += `<div class="text-muted mt-1" style="font-size: 0.8rem; line-height: 1.4;"><i class="bi bi-info-circle"></i> Điều kiện: ${data.terms.replace(/\n/g, '<br/>')}</div>`;
        }
        message.innerHTML = msgHtml;
        message.className = "mt-2 small fw-semibold text-success";
    } catch (err) {
        console.error(err);
        message.innerText = "❌ Lỗi hệ thống khi kiểm tra voucher";
        message.className = "mt-2 small fw-semibold text-danger";
    }
}

// KHỞI TẠO XỬ LÝ OVERLAY TRẠNG THÁI CHỜ THANH TOÁN
document.addEventListener("DOMContentLoaded", () => {
    updatePaymentUI();

    const checkoutForm = document.getElementById("checkoutPaymentForm");
    if (checkoutForm) {
        checkoutForm.addEventListener("submit", function (e) {
            const checkAge = document.getElementById("checkAge");
            const checkTerms = document.getElementById("checkTerms");

            if (checkAge && !checkAge.checked) {
                alert("Vui lòng xác nhận điều kiện về độ tuổi xem phim!");
                checkAge.focus();
                e.preventDefault();
                return false;
            }
            if (checkTerms && !checkTerms.checked) {
                alert("Vui lòng đồng ý với các điều khoản dịch vụ và bảo mật!");
                checkTerms.focus();
                e.preventDefault();
                return false;
            }

            e.preventDefault();

            // Lấy phương thức thanh toán đã chọn
            const checkedInput = document.querySelector('input[name="method"]:checked');
            const method = checkedInput ? checkedInput.value : 'VietQR';

            const overlay = document.getElementById("paymentLoadingOverlay");
            const iconBox = document.getElementById("paymentLoadingIcon");
            const textEl = document.getElementById("paymentLoadingText");

            if (method === 'VietQR') {
                if (iconBox) iconBox.innerHTML = '<i class="bi bi-bank2 text-info"></i>';
                if (textEl) textEl.innerText = "Đang chuyển đến trang thanh toán QR ngân hàng...";
            } else if (method === 'Chuyển khoản') {
                if (iconBox) iconBox.innerHTML = '<i class="bi bi-wallet2 text-primary"></i>';
                if (textEl) textEl.innerText = "Đang kết nối cổng thanh toán trực tuyến VNPAY...";
            } else if (method === 'Online') {
                if (iconBox) iconBox.innerHTML = '<i class="bi bi-credit-card-2-front-fill text-warning"></i>';
                if (textEl) textEl.innerText = "Đang chuyển sang cổng thanh toán thẻ quốc tế Stripe...";
            } else {
                if (iconBox) iconBox.innerHTML = '<i class="bi bi-shop text-success"></i>';
                if (textEl) textEl.innerText = "Đang hoàn tất giữ chỗ và khởi tạo vé...";
            }

            if (overlay) {
                overlay.classList.add("active");
            }

            // Hiệu ứng chuyển cảnh mượt mà 1 giây trước khi gửi dữ liệu
            setTimeout(() => {
                checkoutForm.submit();
            }, 1000);
        });
    }
});
