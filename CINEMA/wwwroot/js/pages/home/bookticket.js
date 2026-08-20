let seatConnection = null;
let currentShowtimeId = 0;

function calcTotal() {
    const basePriceElement = document.getElementById("basePrice");
    if (!basePriceElement) return;

    const basePrice = parseFloat(basePriceElement.value) || 0;
    let adult = parseInt(document.getElementsByName("AdultTickets")[0]?.value) || 0;
    let child = parseInt(document.getElementsByName("ChildTickets")[0]?.value) || 0;
    let student = parseInt(document.getElementsByName("StudentTickets")[0]?.value) || 0;

    let total = adult * basePrice
        + child * (basePrice * 0.7)
        + student * (basePrice * 0.8);

    let surchargeTotal = 0;
    document.querySelectorAll(".seat-checkbox:checked").forEach(cb => {
        let sc = parseFloat(cb.getAttribute("data-surcharge")) || 0;
        surchargeTotal += sc;
    });
    total += surchargeTotal;

    document.querySelectorAll(".combo-input").forEach(el => {
        let qty = parseInt(el.value) || 0;
        let price = parseFloat(el.dataset.price) || 0;
        total += qty * price;
    });

    if (total < 0) total = 0;

    const totalPriceEl = document.getElementById("totalPrice");
    if (totalPriceEl) totalPriceEl.innerText = total.toLocaleString("vi-VN");
    const hiddenTotalEl = document.getElementById("TotalPriceHidden");
    if (hiddenTotalEl) hiddenTotalEl.value = total;
}

function getMaxSeats() {
    let adult = parseInt(document.getElementsByName("AdultTickets")[0]?.value) || 0;
    let child = parseInt(document.getElementsByName("ChildTickets")[0]?.value) || 0;
    let student = parseInt(document.getElementsByName("StudentTickets")[0]?.value) || 0;
    return adult + child + student;
}

function limitSeats() {
    let maxSeats = getMaxSeats();
    let checked = document.querySelectorAll(".seat-checkbox:checked");

    if (maxSeats === 0) {
        alert("Vui lòng chọn số lượng vé trước khi chọn ghế!");
        checked.forEach(cb => cb.checked = false);
        return false;
    }

    if (checked.length > maxSeats) {
        alert("Bạn chỉ được chọn tối đa " + maxSeats + " ghế!");
        checked[checked.length - 1].checked = false;
        return false;
    }

    return true;
}

function updateSelectedSeats() {
    let selected = [];
    document.querySelectorAll(".seat-checkbox:checked").forEach(cb => {
        selected.push(cb.value);
    });

    const selectedSeatsElem = document.getElementById("selectedSeats");
    if(selectedSeatsElem) selectedSeatsElem.value = selected.join(",");

    let displayDiv = document.getElementById("displaySelectedSeats");
    if (displayDiv) {
        if (selected.length > 0) {
            displayDiv.innerHTML = selected.map(code => `<span class="seat-badge-selected">${code}</span>`).join(" ");
        } else {
            displayDiv.innerHTML = '<span class="text-muted small">Chưa chọn ghế</span>';
        }
    }
}

function validateForm() {
    updateSelectedSeats();

    let maxSeats = getMaxSeats();
    let selected = document.getElementById("selectedSeats")?.value;

    if (maxSeats === 0) {
        alert("Vui lòng chọn ít nhất 1 vé để tiếp tục!");
        return false;
    }

    if (!selected) {
        alert("Bạn chưa chọn vị trí ghế ngồi!");
        return false;
    }

    if (selected.split(",").length !== maxSeats) {
        alert("Vui lòng chọn đủ " + maxSeats + " ghế tương ứng với số vé!");
        return false;
    }

    return true;
}

function setSeatHeldByOther(seatCode, isHeld) {
    const cb = document.querySelector(`.seat-checkbox[value='${seatCode}']`);
    if (!cb) return;
    const wrapper = cb.closest('.seat-wrapper');
    if (!wrapper) return;

    if (isHeld) {
        cb.disabled = true;
        if (cb.checked) {
            cb.checked = false;
            updateSelectedSeats();
            calcTotal();
        }
        wrapper.classList.add('seat-held-by-other');
        wrapper.title = `${seatCode} - Đang có người chọn`;
    } else {
        if (!wrapper.classList.contains('disabled-seat')) {
            cb.disabled = false;
            wrapper.classList.remove('seat-held-by-other');
            wrapper.title = `${seatCode}`;
        }
    }
}

function initRealtimeSeatHub() {
    const showtimeInput = document.querySelector("input[name='ShowtimeId']");
    if (!showtimeInput || !window.signalR) return;

    currentShowtimeId = parseInt(showtimeInput.value) || 0;
    if (currentShowtimeId <= 0) return;

    seatConnection = new signalR.HubConnectionBuilder()
        .withUrl("/seatHub")
        .withAutomaticReconnect()
        .build();

    seatConnection.on("CurrentHeldSeats", function (heldByOthers, myHeldSeats) {
        if (heldByOthers && Array.isArray(heldByOthers)) {
            heldByOthers.forEach(seatCode => setSeatHeldByOther(seatCode, true));
        }
    });

    seatConnection.on("SeatHeldByOther", function (seatCode) {
        setSeatHeldByOther(seatCode, true);
    });

    seatConnection.on("SeatReleasedByOther", function (seatCode) {
        setSeatHeldByOther(seatCode, false);
    });

    seatConnection.on("SeatHoldFailed", function (seatCode, message) {
        alert(message || "Ghế này vừa có người chọn!");
        setSeatHeldByOther(seatCode, true);
    });

    seatConnection.start().then(function () {
        seatConnection.invoke("JoinShowtimeRoom", currentShowtimeId)
            .catch(err => console.error("Error joining showtime room: ", err));
    }).catch(function (err) {
        console.error("SignalR SeatHub connection failed: ", err);
    });
}

document.addEventListener("DOMContentLoaded", function () {
    if(document.getElementById("bookingForm")) {
        document.querySelectorAll(".seat-checkbox").forEach(cb => {
            cb.addEventListener("change", function () {
                const wasChecked = this.checked;
                const allowed = limitSeats();
                updateSelectedSeats();
                calcTotal();

                if (seatConnection && seatConnection.state === signalR.HubConnectionState.Connected && currentShowtimeId > 0) {
                    if (this.checked) {
                        seatConnection.invoke("HoldSeat", currentShowtimeId, this.value)
                            .catch(err => console.error("HoldSeat error:", err));
                    } else if (!wasChecked || !allowed) {
                        seatConnection.invoke("ReleaseSeat", currentShowtimeId, this.value)
                            .catch(err => console.error("ReleaseSeat error:", err));
                    }
                }
            });
        });

        document.querySelectorAll(".ticket-input").forEach(el => {
            el.addEventListener("input", function () {
                let adult = parseInt(document.getElementsByName("AdultTickets")[0]?.value) || 0;
                let child = parseInt(document.getElementsByName("ChildTickets")[0]?.value) || 0;
                let student = parseInt(document.getElementsByName("StudentTickets")[0]?.value) || 0;
                let sum = adult + child + student;

                if (sum > 10) {
                    alert("Bạn chỉ được đặt tối đa 10 vé cho mỗi đơn hàng!");
                    let currentVal = parseInt(el.value) || 0;
                    let otherSum = sum - currentVal;
                    let allowedVal = 10 - otherSum;
                    if (allowedVal < 0) allowedVal = 0;
                    el.value = allowedVal;
                }

                // Release all checked seats via SignalR
                document.querySelectorAll(".seat-checkbox:checked").forEach(cb => {
                    if (seatConnection && seatConnection.state === signalR.HubConnectionState.Connected && currentShowtimeId > 0) {
                        seatConnection.invoke("ReleaseSeat", currentShowtimeId, cb.value);
                    }
                    cb.checked = false;
                });
                updateSelectedSeats();
                calcTotal();
            });
        });

        document.querySelectorAll(".combo-input").forEach(el => {
            el.addEventListener("input", function () {
                calcTotal();
            });
        });

        let timeLeft = 600;
        setInterval(() => {
            if (timeLeft <= 0) return;
            timeLeft--;
            let m = Math.floor(timeLeft / 60);
            let s = timeLeft % 60;
            const countdownElem = document.getElementById("countdown");
            if (countdownElem) countdownElem.innerText = `${m}:${s.toString().padStart(2, '0')}`;
        }, 1000);

        calcTotal();
        updateSelectedSeats();
        initRealtimeSeatHub();
    }
});

window.shareFacebook = function(url) {
    window.open('https://www.facebook.com/sharer/sharer.php?u=' + encodeURIComponent(url), 'sharer', 'toolbar=0,status=0,width=626,height=436');
};

window.copyShareLink = function(button) {
    const url = window.location.href;

    const showSuccess = () => {
        const originalContent = button.innerHTML;
        button.innerHTML = '<i class="bi bi-check2"></i> Đã chép!';
        button.classList.add('btn-share-success');
        setTimeout(() => {
            button.innerHTML = originalContent;
            button.classList.remove('btn-share-success');
        }, 2000);
    };

    if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(url).then(showSuccess).catch(err => {
            fallbackCopyText(url, showSuccess);
        });
    } else {
        fallbackCopyText(url, showSuccess);
    }
};

function fallbackCopyText(text, callback) {
    const textArea = document.createElement("textarea");
    textArea.value = text;
    textArea.style.top = "0";
    textArea.style.left = "0";
    textArea.style.position = "fixed";
    textArea.style.opacity = "0";
    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();

    try {
        const successful = document.execCommand('copy');
        if (successful) {
            callback();
        } else {
            console.error('execCommand copy không thành công');
            alert('Không thể tự động sao chép. Bạn hãy copy thủ công từ thanh địa chỉ nhé!');
        }
    } catch (err) {
        console.error('Lỗi khi sao chép liên kết bằng execCommand: ', err);
        alert('Không thể tự động sao chép. Bạn hãy copy thủ công từ thanh địa chỉ nhé!');
    }

    document.body.removeChild(textArea);
}

function autoPickBestSeats() {
    let maxSeats = getMaxSeats();
    if (maxSeats <= 0) {
        const adultInput = document.getElementsByName("AdultTickets")[0];
        if (adultInput) {
            adultInput.value = 1;
        }
        maxSeats = 1;
    }

    const availableSeats = [];
    document.querySelectorAll(".seat-checkbox:not(:disabled)").forEach(cb => {
        const wrapper = cb.closest(".seat-wrapper");
        if (wrapper && !wrapper.classList.contains("seat-held-by-other")) {
            const seatCode = cb.value;
            const rowLabel = seatCode.match(/^[A-Z]+/)?.[0] || "";
            const seatNum = parseInt(seatCode.replace(/^[A-Z]+/, "")) || 0;
            const isVip = wrapper.querySelector(".seat-item")?.classList.contains("seat-vip") || false;
            availableSeats.push({
                element: cb,
                code: seatCode,
                row: rowLabel,
                num: seatNum,
                isVip: isVip
            });
        }
    });

    if (availableSeats.length < maxSeats) {
        alert("Sơ đồ phòng chiếu không còn đủ " + maxSeats + " ghế trống.");
        return;
    }

    const rows = [...new Set(availableSeats.map(s => s.row))].sort();
    const midRowIndex = Math.floor(rows.length / 2);
    const maxSeatNum = Math.max(...availableSeats.map(s => s.num));
    const minSeatNum = Math.min(...availableSeats.map(s => s.num));
    const centerSeatNum = (maxSeatNum + minSeatNum) / 2.0;

    let bestBlock = null;
    let minScore = Infinity;

    const seatsByRow = {};
    availableSeats.forEach(s => {
        if (!seatsByRow[s.row]) seatsByRow[s.row] = [];
        seatsByRow[s.row].push(s);
    });

    Object.keys(seatsByRow).forEach(rowLabel => {
        const rowSeats = seatsByRow[rowLabel].sort((a, b) => a.num - b.num);
        const rowIndex = rows.indexOf(rowLabel);
        const rowDist = Math.abs(rowIndex - midRowIndex);

        for (let i = 0; i <= rowSeats.length - maxSeats; i++) {
            const block = rowSeats.slice(i, i + maxSeats);
            let isConsecutive = true;
            for (let j = 0; j < block.length - 1; j++) {
                if (block[j + 1].num - block[j].num !== 1) {
                    isConsecutive = false;
                    break;
                }
            }
            if (!isConsecutive) continue;

            const blockCenter = (block[0].num + block[block.length - 1].num) / 2.0;
            const seatDist = Math.abs(blockCenter - centerSeatNum);
            const vipBonus = block.some(s => s.isVip) ? -1 : 0;
            const score = (rowDist * 3) + seatDist + vipBonus;

            if (score < minScore) {
                minScore = score;
                bestBlock = block;
            }
        }
    });

    if (!bestBlock) {
        availableSeats.sort((a, b) => {
            const rowDistA = Math.abs(rows.indexOf(a.row) - midRowIndex);
            const rowDistB = Math.abs(rows.indexOf(b.row) - midRowIndex);
            const seatDistA = Math.abs(a.num - centerSeatNum);
            const seatDistB = Math.abs(b.num - centerSeatNum);
            return (rowDistA * 3 + seatDistA) - (rowDistB * 3 + seatDistB);
        });
        bestBlock = availableSeats.slice(0, maxSeats);
    }

    document.querySelectorAll(".seat-checkbox").forEach(cb => {
        if (cb.checked) {
            cb.checked = false;
            let code = cb.value;
            let currentSelected = (document.getElementById("selectedSeats")?.value || "").split(",").filter(Boolean);
            let idx = currentSelected.indexOf(code);
            if (idx > -1) {
                currentSelected.splice(idx, 1);
                document.getElementById("selectedSeats").value = currentSelected.join(",");
            }
            if (typeof seatConnection !== 'undefined' && seatConnection && currentShowtimeId > 0) {
                seatConnection.invoke("ReleaseSeat", currentShowtimeId, code).catch(err => console.error(err));
            }
        }
    });

    const chosenCodes = [];
    bestBlock.forEach(s => {
        s.element.checked = true;
        let code = s.code;
        chosenCodes.push(code);
        let currentSelected = (document.getElementById("selectedSeats")?.value || "").split(",").filter(Boolean);
        if (!currentSelected.includes(code)) {
            currentSelected.push(code);
            document.getElementById("selectedSeats").value = currentSelected.join(",");
        }
        if (typeof seatConnection !== 'undefined' && seatConnection && currentShowtimeId > 0) {
            seatConnection.invoke("HoldSeat", currentShowtimeId, code).catch(err => console.error(err));
        }
    });

    calcTotal();
    alert(`✨ CineZone đã tự động chọn ${maxSeats} ghế đẹp nhất gần trung tâm: ${chosenCodes.join(", ")}!`);
}
