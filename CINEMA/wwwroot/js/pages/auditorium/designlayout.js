let activeTool = "Standard";
let isDrawing = false;
let gridRows = (window.DesignLayoutConfig && window.DesignLayoutConfig.seatRows) ? window.DesignLayoutConfig.seatRows : 10;
let gridCols = (window.DesignLayoutConfig && window.DesignLayoutConfig.seatCols) ? window.DesignLayoutConfig.seatCols : 15;
let seatsData = (window.DesignLayoutConfig && window.DesignLayoutConfig.existingSeats) ? window.DesignLayoutConfig.existingSeats : {};

document.addEventListener("DOMContentLoaded", () => {
    buildGrid();
    
    window.addEventListener("mouseup", () => {
        isDrawing = false;
    });
});

function selectTool(type) {
    activeTool = type;
    document.querySelectorAll(".tool-btn").forEach(btn => {
        if (btn.getAttribute("data-type") === type) {
            btn.classList.add("active");
        } else {
            btn.classList.remove("active");
        }
    });
}

function buildGrid() {
    const grid = document.getElementById("seatGrid");
    if (!grid) return;
    grid.innerHTML = "";
    
    grid.style.gridTemplateColumns = `40px repeat(${gridCols}, 42px)`;
    
    for (let r = 1; r <= gridRows; r++) {
        const rowLabel = String.fromCharCode(64 + r);
        
        const rowHeader = document.createElement("div");
        rowHeader.className = "row-header-label";
        rowHeader.textContent = rowLabel;
        grid.appendChild(rowHeader);
        
        for (let c = 1; c <= gridCols; c++) {
            const key = `${rowLabel}_${c}`;
            const cellData = seatsData[key] || { type: "Empty", active: true };
            
            const cell = document.createElement("div");
            cell.className = `seat-cell seat-${cellData.type}`;
            cell.dataset.row = rowLabel;
            cell.dataset.col = c;
            
            cell.textContent = cellData.type !== "Empty" ? c : "";
            
            cell.addEventListener("mousedown", (e) => {
                e.preventDefault();
                isDrawing = true;
                applyTool(cell);
            });
            
            cell.addEventListener("mouseenter", () => {
                if (isDrawing) {
                    applyTool(cell);
                }
            });
            
            grid.appendChild(cell);
        }
    }
}

function applyTool(cell) {
    const row = cell.dataset.row;
    const col = cell.dataset.col;
    const key = `${row}_${col}`;
    
    cell.className = `seat-cell seat-${activeTool}`;
    cell.textContent = activeTool !== "Empty" ? col : "";
    
    if (activeTool === "Empty") {
        delete seatsData[key];
    } else {
        seatsData[key] = {
            type: activeTool,
            active: true
        };
    }
}

function resizeGrid() {
    const rInput = parseInt(document.getElementById("gridRows").value) || 10;
    const cInput = parseInt(document.getElementById("gridCols").value) || 15;
    
    if (rInput < 1 || rInput > 26 || cInput < 1 || cInput > 30) {
        alert("Kích thước lưới không hợp lệ (Hàng: 1-26, Cột: 1-30)");
        return;
    }
    
    gridRows = rInput;
    gridCols = cInput;
    buildGrid();
}

async function saveLayout() {
    const seatsList = [];
    const auditoriumId = window.DesignLayoutConfig ? window.DesignLayoutConfig.auditoriumId : 0;
    
    for (const [key, seat] of Object.entries(seatsData)) {
        const [row, col] = key.split("_");
        seatsList.push({
            rowLabel: row,
            seatNumber: parseInt(col),
            seatType: seat.type,
            isActive: seat.active
        });
    }
    
    if (seatsList.length === 0) {
        if (!confirm("Cảnh báo: Bạn đang lưu sơ đồ rỗng (không có ghế nào). Xác nhận lưu?")) {
            return;
        }
    }
    
    try {
        const res = await fetch(`/Auditorium/SaveLayout/${auditoriumId}`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(seatsList)
        });
        const data = await res.json();
        
        if (data.success) {
            alert("✅ " + data.message);
            window.location.reload();
        } else {
            alert("❌ Lỗi: " + data.message);
        }
    } catch (err) {
        alert("❌ Lỗi hệ thống khi gửi dữ liệu!");
    }
}
