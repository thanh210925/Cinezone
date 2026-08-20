function enableCombos() {
    const theaterSelect = document.getElementById('theaterId');
    const wrapper = document.getElementById('combos-wrapper');
    
    if (theaterSelect && wrapper) {
        if (theaterSelect.value !== '') {
            wrapper.style.pointerEvents = 'auto';
            wrapper.classList.remove('opacity-50');
        } else {
            wrapper.style.pointerEvents = 'none';
            wrapper.classList.add('opacity-50');
            const inputs = document.querySelectorAll('.qty-input');
            inputs.forEach(input => {
                input.value = 0;
            });
            updateTotal();
        }
    }
}

function changeQty(comboId, val) {
    const input = document.getElementById('qty-' + comboId);
    if (!input) return;
    let curr = parseInt(input.value) || 0;
    curr += val;
    if (curr < 0) curr = 0;
    input.value = curr;
    updateTotal();
}

function updateTotal() {
    const inputs = document.querySelectorAll('.qty-input');
    let subtotal = 0;
    const billContainer = document.getElementById('bill-items');
    if (!billContainer) return;
    
    let htmlItems = '';
    let hasItems = false;

    inputs.forEach(input => {
        const qty = parseInt(input.value) || 0;
        const price = parseFloat(input.getAttribute('data-price')) || 0;
        const name = input.getAttribute('data-name');
        
        if (qty > 0) {
            hasItems = true;
            const cost = price * qty;
            subtotal += cost;
            
            htmlItems += `
                <div class="d-flex justify-content-between align-items-center mb-2 bill-item-row">
                    <div>
                        <span class="fw-bold" style="font-size: 0.9rem;">${name}</span>
                        <span class="text-muted small d-block" style="font-size: 0.75rem;">Số lượng: ${qty}</span>
                    </div>
                    <span class="fw-bold">${cost.toLocaleString('vi-VN')} đ</span>
                </div>
            `;
        }
    });

    if (hasItems) {
        billContainer.innerHTML = htmlItems;
    } else {
        billContainer.innerHTML = `
            <div class="text-center py-4 text-muted small" id="no-items-text">
                <i class="bi bi-cart3 fs-2 d-block mb-2 opacity-50"></i>
                Chưa chọn bắp nước nào.
            </div>
        `;
    }

    const subtotalEl = document.getElementById('subtotal-val');
    if (subtotalEl) subtotalEl.innerText = subtotal.toLocaleString('vi-VN') + ' đ';
    const totalEl = document.getElementById('total-val');
    if (totalEl) totalEl.innerText = subtotal.toLocaleString('vi-VN') + ' đ';
}

document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById('concessionForm');
    if (form) {
        form.addEventListener('submit', function(e) {
            const theaterSelect = document.getElementById('theaterId');
            if (theaterSelect && theaterSelect.value === '') {
                e.preventDefault();
                alert('Vui lòng chọn chi nhánh rạp nhận bắp nước!');
                return;
            }

            const inputs = document.querySelectorAll('.qty-input');
            let selectedCount = 0;
            inputs.forEach(input => {
                selectedCount += parseInt(input.value) || 0;
            });

            if (selectedCount <= 0) {
                e.preventDefault();
                alert('Vui lòng chọn ít nhất 1 sản phẩm bắp nước!');
            }
        });
    }
});
