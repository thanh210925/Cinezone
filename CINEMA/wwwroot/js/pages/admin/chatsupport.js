let activeCustomerId = null;
let connection = null;

document.addEventListener("DOMContentLoaded", function () {
    loadActiveChats();
    setupSignalR();

    // Nhấn nút gửi tin nhắn
    const btnSendMsg = document.getElementById("btn-send-message");
    if (btnSendMsg) btnSendMsg.addEventListener("click", sendAdminMessage);
    
    const inputMsg = document.getElementById("admin-message-input");
    if (inputMsg) {
        inputMsg.addEventListener("keypress", function (e) {
            if (e.key === "Enter") sendAdminMessage();
        });
    }

    // Tìm kiếm khách hàng
    const searchInput = document.getElementById("search-customer");
    if (searchInput) {
        searchInput.addEventListener("input", function(e) {
            let keyword = e.target.value.toLowerCase().trim();
            let items = document.querySelectorAll(".customer-item");
            items.forEach(item => {
                let name = item.querySelector(".customer-name").innerText.toLowerCase();
                if (name.includes(keyword)) {
                    item.style.setProperty("display", "flex", "important");
                } else {
                    item.style.setProperty("display", "none", "important");
                }
            });
        });
    }
});

// Thiết lập kết nối SignalR
function setupSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveMessageFromCustomer", function (customerId, customerName, messageText, time, isFirstMessage) {
        if (activeCustomerId === customerId) {
            appendMessage("customer", messageText, time);
            markAsRead(customerId);
        } else {
            updateUnreadCount(customerId, 1);
        }

        updateCustomerLastMessage(customerId, messageText, time);

        if (isFirstMessage) {
            showToastNotification(`🎉 Khách hàng mới: ${customerName}`, `Vừa gửi tin nhắn: "${messageText}"`);
        } else {
            showToastNotification(`✉️ Tin nhắn mới từ ${customerName}`, messageText);
        }
    });

    connection.on("ReceiveMessageFromAdminToCustomer", function (customerId, adminName, messageText, time) {
        if (activeCustomerId === customerId) {
            appendMessage("admin", messageText, time);
        }
        updateCustomerLastMessage(customerId, messageText, time);
    });

    connection.start()
        .then(function () {
            const connStatus = document.getElementById("connection-status");
            if (connStatus) {
                connStatus.innerText = "Đã kết nối trực tuyến";
                connStatus.className = "badge bg-success p-2";
            }
        })
        .catch(function (err) {
            console.error("SignalR connection failed: ", err);
            const connStatus = document.getElementById("connection-status");
            if (connStatus) {
                connStatus.innerText = "Mất kết nối";
                connStatus.className = "badge bg-danger p-2";
            }
        });
}

// Tải danh sách khách hàng đang chat
function loadActiveChats() {
    fetch('/Admin/GetActiveChats')
        .then(res => res.json())
        .then(data => {
            let container = document.getElementById("customer-list-container");
            if (!container) return;
            container.innerHTML = "";

            if (data.length === 0) {
                container.innerHTML = `<div class="text-center py-4 text-muted">Không có cuộc trò chuyện nào.</div>`;
                return;
            }

            data.forEach(chat => {
                let activeClass = chat.customerId === activeCustomerId ? "active" : "";
                let badgeHtml = chat.unreadCount > 0 
                    ? `<span class="badge bg-danger rounded-pill unread-badge">${chat.unreadCount}</span>` 
                    : `<span class="badge bg-danger rounded-pill unread-badge" style="display:none;">0</span>`;

                let item = `
                    <div class="customer-item d-flex align-items-center p-3 ${activeClass}" id="customer-${chat.customerId}" onclick="selectCustomer(${chat.customerId}, '${chat.customerName}', '${chat.avatarUrl}')">
                        <img src="${chat.avatarUrl}" class="rounded-circle me-3" width="40" height="40" alt="Avatar">
                        <div class="flex-grow-1 min-width-0">
                            <div class="d-flex justify-content-between align-items-center mb-1">
                                <h6 class="mb-0 text-white font-weight-bold customer-name text-truncate" style="max-width: 150px;">${chat.customerName}</h6>
                                <small class="chat-time-lbl" style="font-size: 11px;">${chat.lastMessageTime}</small>
                            </div>
                            <p class="text-muted mb-0 small last-message text-truncate">${chat.lastMessage}</p>
                        </div>
                        <div class="ms-2">
                            ${badgeHtml}
                        </div>
                    </div>
                `;
                container.insertAdjacentHTML('beforeend', item);
            });
        });
}

// Chọn khách hàng để chat
function selectCustomer(customerId, customerName, avatarUrl) {
    activeCustomerId = customerId;

    document.querySelectorAll(".customer-item").forEach(item => item.classList.remove("active"));
    let selectedItem = document.getElementById(`customer-${customerId}`);
    if (selectedItem) {
        selectedItem.classList.add("active");
        let badge = selectedItem.querySelector(".unread-badge");
        if (badge) {
            badge.style.display = "none";
            badge.innerText = "0";
        }
    }

    const chatHeader = document.getElementById("chat-header");
    if (chatHeader) chatHeader.style.setProperty("display", "flex", "important");
    const chatInputArea = document.getElementById("chat-input-area");
    if (chatInputArea) chatInputArea.style.setProperty("display", "block", "important");
    
    const chatCustName = document.getElementById("chat-customer-name");
    if (chatCustName) chatCustName.innerText = customerName;
    const chatAvatar = document.getElementById("chat-avatar");
    if (chatAvatar) chatAvatar.src = avatarUrl;

    let messagesContainer = document.getElementById("chat-messages-container");
    if (messagesContainer) {
        messagesContainer.innerHTML = `<div class="m-auto text-center text-muted">Đang tải lịch sử chat...</div>`;

        fetch(`/Admin/GetChatHistoryAdmin?customerId=${customerId}`)
            .then(res => res.json())
            .then(data => {
                messagesContainer.innerHTML = "";
                if (data.success && data.messages.length > 0) {
                    data.messages.forEach(msg => {
                        appendMessage(msg.type, msg.text, msg.time);
                    });
                } else {
                    messagesContainer.innerHTML = `<div class="m-auto text-center text-muted">Không có tin nhắn nào. Gửi tin nhắn đầu tiên hỗ trợ khách hàng!</div>`;
                }
                scrollToBottom();
            });
    }
}

// Thêm tin nhắn mới vào giao diện
function appendMessage(type, text, time) {
    let container = document.getElementById("chat-messages-container");
    if (!container) return;
    
    let placeholder = container.querySelector(".m-auto");
    if (placeholder) placeholder.remove();

    let bubble = document.createElement("div");
    bubble.className = `chat-bubble ${type}`;
    bubble.innerHTML = `${text}<span class="chat-time">${time}</span>`;
    
    container.appendChild(bubble);
    scrollToBottom();
}

// Gửi tin nhắn từ Admin
function sendAdminMessage() {
    let input = document.getElementById("admin-message-input");
    if (!input) return;
    let text = input.value.trim();

    if (!text || activeCustomerId === null) return;

    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("SendMessageFromAdmin", activeCustomerId, text)
            .then(() => {
                input.value = "";
                scrollToBottom();
            })
            .catch(err => console.error("Gửi tin nhắn thất bại: ", err));
    } else {
        alert("Mất kết nối với máy chủ SignalR. Vui lòng tải lại trang.");
    }
}

// Cuộn xuống cuối khung tin nhắn
function scrollToBottom() {
    let container = document.getElementById("chat-messages-container");
    if (container) container.scrollTop = container.scrollHeight;
}

// Cập nhật nội dung tin nhắn cuối ngoài danh sách
function updateCustomerLastMessage(customerId, text, time) {
    let item = document.getElementById(`customer-${customerId}`);
    if (item) {
        item.querySelector(".last-message").innerText = text;
        item.querySelector(".chat-time-lbl").innerText = time;

        let container = document.getElementById("customer-list-container");
        if (container) container.prepend(item);
    } else {
        loadActiveChats();
    }
}

// Đổi số tin nhắn chưa đọc
function updateUnreadCount(customerId, increment) {
    let item = document.getElementById(`customer-${customerId}`);
    if (item) {
        let badge = item.querySelector(".unread-badge");
        if (badge) {
            let currentCount = parseInt(badge.innerText) || 0;
            let newCount = currentCount + increment;
            badge.innerText = newCount;
            if (newCount > 0) {
                badge.style.display = "inline-block";
            } else {
                badge.style.display = "none";
            }
        }
    }
}

// Đánh dấu đã đọc
function markAsRead(customerId) {
    fetch(`/Admin/GetChatHistoryAdmin?customerId=${customerId}`);
}

// Hiển thị thông báo Notification (Toast)
function showToastNotification(title, message) {
    if (Notification.permission === "granted") {
        new Notification(title, { body: message });
    } else if (Notification.permission !== "denied") {
        Notification.requestPermission().then(permission => {
            if (permission === "granted") {
                new Notification(title, { body: message });
            }
        });
    }

    if (window.showGlobalToast) {
        window.showGlobalToast(title, message);
    }
}
