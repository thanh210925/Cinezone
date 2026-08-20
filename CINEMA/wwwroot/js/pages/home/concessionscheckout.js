let discountAmt = 0;
const baseTotal = (window.ConcessionsCheckoutConfig && window.ConcessionsCheckoutConfig.baseTotal) ? parseFloat(window.ConcessionsCheckoutConfig.baseTotal) : 0;

function applyVoucher() {
    const codeInput = document.getElementById("voucherCode");
    if (!codeInput) return;
    const code = codeInput.value.trim();
    const statusMsg = document.getElementById("voucher-status-msg");
    if (!statusMsg) return;

    if (!code) {
        statusMsg.className = "small mt-2 px-1 text-danger fw-semibold";
        statusMsg.innerText = "Vui lòng nhập mã voucher!";
        return;
    }

    statusMsg.className = "small mt-2 px-1 text-warning fw-semibold";
    statusMsg.innerText = "Đang kiểm tra...";

    fetch(`/Home/CheckVoucher?code=${encodeURIComponent(code)}&total=${baseTotal}`)
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                discountAmt = parseFloat(data.discount) || 0;
                statusMsg.className = "small mt-2 px-1 text-success fw-semibold";
                let msgHtml = `Áp dụng thành công! Được giảm ${discountAmt.toLocaleString("vi-VN")} đ`;
                if (data.terms) {
                    msgHtml += `<br/><small class="text-muted d-block mt-1" style="font-size: 0.8rem; line-height: 1.4;"><i class="bi bi-info-circle"></i> Điều kiện: ${data.terms.replace(/\n/g, '<br/>')}</small>`;
                }
                statusMsg.innerHTML = msgHtml;
                recalculateTotal();
            } else {
                discountAmt = 0;
                statusMsg.className = "small mt-2 px-1 text-danger fw-semibold";
                statusMsg.innerText = `Lỗi: ${data.message}`;
                recalculateTotal();
            }
        })
        .catch(err => {
            console.error(err);
            discountAmt = 0;
            statusMsg.className = "small mt-2 px-1 text-danger fw-semibold";
            statusMsg.innerText = "Lỗi đường truyền hoặc máy chủ!";
            recalculateTotal();
        });
}

function recalculateTotal() {
    const discountEl = document.getElementById("summary-discount");
    const totalEl = document.getElementById("summary-total");

    if (discountEl) discountEl.innerText = "-" + discountAmt.toLocaleString("vi-VN") + " đ";
    
    let finalTotal = baseTotal - discountAmt;
    if (finalTotal < 0) finalTotal = 0;
    
    if (totalEl) totalEl.innerText = finalTotal.toLocaleString("vi-VN") + " đ";
}
