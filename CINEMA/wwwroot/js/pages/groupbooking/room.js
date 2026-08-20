const roomId = (window.RoomConfig && window.RoomConfig.roomId) ? window.RoomConfig.roomId : '';
const currentCustomerId = (window.RoomConfig && window.RoomConfig.currentCustomerId) ? window.RoomConfig.currentCustomerId : 0;
let expiresAt = (window.RoomConfig && window.RoomConfig.expiresAt) ? new Date(window.RoomConfig.expiresAt) : new Date();

document.addEventListener("DOMContentLoaded", function () {
    const shareLinkInput = document.getElementById("shareRoomLink");
    if (shareLinkInput) shareLinkInput.value = window.location.href;

    if (window.signalR) {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/groupBookingHub")
            .withAutomaticReconnect()
            .build();

        connection.on("UserJoinedRoom", function () { refreshRoomData(); });
        connection.on("SeatSelectedByMember", function () { refreshRoomData(); });
        connection.on("SeatReleasedByMember", function () { refreshRoomData(); });
        connection.on("MemberPaid", function () { refreshRoomData(); });

        connection.start().then(function () {
            connection.invoke("JoinRoom", roomId).catch(err => console.error(err));
        }).catch(function (err) {
            console.error("SignalR Connection Error: ", err.toString());
        });
    }

    refreshRoomData();
});

function selectGroupSeat(seatId) {
    const btn = document.getElementById("btn-seat-" + seatId);
    if (!btn) return;
    
    let targetSeatId = seatId;
    if (btn.classList.contains("seat-my-selected")) {
        targetSeatId = null;
    }

    fetch(`/GroupBooking/ConfirmSeat?roomId=${roomId}&seatId=${targetSeatId || ""}`, {
        method: "POST"
    })
    .then(res => {
        if (!res.ok) {
            res.text().then(text => alert(text));
        }
    })
    .catch(err => console.error("Lỗi chọn ghế: ", err));
}

function refreshRoomData() {
    fetch(`/GroupBooking/RoomStatus?roomId=${roomId}`)
    .then(res => res.json())
    .then(data => {
        if (data.status === "Expired") {
            alert("Phòng chờ đặt vé nhóm này đã hết hạn!");
            window.location.href = "/Home/Index";
            return;
        }
        
        renderMembers(data.members);
        updateSeatingChart(data.members);
    })
    .catch(err => console.error("Lỗi tải thông tin phòng: ", err));
}

function renderMembers(members) {
    const container = document.getElementById("memberListContainer");
    if (!container) return;
    container.innerHTML = "";

    let mySeatCode = "Chưa chọn";
    let myHasPaid = false;

    members.forEach(member => {
        const isMe = member.customerId === currentCustomerId;
        if (isMe) {
            mySeatCode = member.seatCode !== "Chưa chọn" ? member.seatCode : "Chưa chọn";
            myHasPaid = member.status === "Paid";
        }

        let statusBadge = "";
        if (member.status === "Paid") {
            statusBadge = `<span class="badge bg-success"><i class="bi bi-check-circle-fill"></i> Đã thanh toán</span>`;
        } else if (member.status === "SeatSelected") {
            statusBadge = `<span class="badge bg-info">Đã chọn: ${member.seatCode}</span>`;
        } else {
            statusBadge = `<span class="badge bg-secondary opacity-75">Đang chọn ghế...</span>`;
        }

        const cardHtml = `
            <div class="d-flex align-items-center justify-content-between p-3 rounded-3 member-card ${isMe ? 'border-primary border-opacity-50 border' : ''}">
                <div class="d-flex align-items-center gap-3">
                    <img src="${member.avatarUrl}" class="member-avatar" alt="Avatar" />
                    <div>
                        <h6 class="fw-bold text-white mb-0 small">
                            ${member.fullName} 
                            ${member.isCreator ? '<span class="text-warning text-xs ms-1">★ Trưởng nhóm</span>' : ''}
                            ${isMe ? '<span class="text-primary text-xs ms-1">(Bạn)</span>' : ''}
                        </h6>
                        <span class="text-muted small mt-1 d-block" style="font-size: 0.72rem;">Ghế: ${member.seatCode}</span>
                    </div>
                </div>
                <div>
                    ${statusBadge}
                </div>
            </div>
        `;
        container.innerHTML += cardHtml;
    });

    const mySeatDisplay = document.getElementById("mySeatCodeDisplay");
    if (mySeatDisplay) mySeatDisplay.innerText = mySeatCode;
    
    const btnCheckout = document.getElementById("btnCheckout");
    if (btnCheckout) {
        if (mySeatCode !== "Chưa chọn" && !myHasPaid) {
            btnCheckout.classList.remove("disabled");
        } else {
            btnCheckout.classList.add("disabled");
        }

        if (myHasPaid) {
            btnCheckout.innerText = "BẠN ĐÃ THANH TOÁN XONG ✅";
            btnCheckout.className = "btn btn-success w-100 btn-lg fw-bold rounded-3 shadow-lg py-3 disabled";
        } else {
            btnCheckout.innerText = "THANH TOÁN PHẦN CỦA TÔI";
            btnCheckout.className = "btn btn-danger w-100 btn-lg fw-bold rounded-3 shadow-lg py-3" + (mySeatCode === "Chưa chọn" ? " disabled" : "");
        }
    }
}

function updateSeatingChart(members) {
    document.querySelectorAll(".seat-container").forEach(el => {
        const isBooked = el.getAttribute("data-booked") === "true";
        if (!isBooked) {
            const btn = el.querySelector(".btn-seat");
            if (btn) btn.classList.remove("seat-my-selected", "seat-group-selected");
            
            const badge = el.querySelector(".seat-member-badge");
            if (badge) {
                badge.style.display = "none";
                badge.innerText = "";
            }
        }
    });

    members.forEach(member => {
        if (member.seatId > 0) {
            const el = document.getElementById("seat-container-" + member.seatId);
            if (el) {
                const isMe = member.customerId === currentCustomerId;
                const btn = el.querySelector(".btn-seat");
                const badge = el.querySelector(".seat-member-badge");

                if (isMe && btn) {
                    btn.classList.add("seat-my-selected");
                } else if (btn) {
                    btn.classList.add("seat-group-selected");
                    const initials = member.fullName ? member.fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase() : '';
                    if (badge) {
                        badge.innerText = initials;
                        badge.style.display = "block";
                    }
                }
            }
        }
    });
}

const countdownTimer = setInterval(function () {
    const now = new Date().getTime();
    const distance = expiresAt - now;

    const countdownEl = document.getElementById("roomCountdown");

    if (distance < 0) {
        clearInterval(countdownTimer);
        if (countdownEl) countdownEl.innerText = "HẾT HẠN";
        alert("Thời gian giữ phòng chờ đã hết!");
        window.location.href = "/Home/Index";
        return;
    }

    const minutes = Math.floor((distance % (1000 * 60 * 60)) / (1000 * 60));
    const seconds = Math.floor((distance % (1000 * 60)) / 1000);

    if (countdownEl) {
        countdownEl.innerText = minutes + ":" + (seconds < 10 ? "0" + seconds : seconds);
    }
}, 1000);

function copyRoomLink(btn) {
    const copyText = document.getElementById("shareRoomLink");
    if (!copyText) return;
    copyText.select();
    copyText.setSelectionRange(0, 99999);
    
    navigator.clipboard.writeText(copyText.value).then(function() {
        const originalText = btn.innerHTML;
        btn.innerHTML = '<i class="bi bi-check2"></i> Copied!';
        btn.className = "btn btn-success px-3 rounded-3 btn-sm";
        setTimeout(() => {
            btn.innerHTML = originalText;
            btn.className = "btn btn-primary px-3 rounded-3 btn-sm";
        }, 2000);
    });
}
