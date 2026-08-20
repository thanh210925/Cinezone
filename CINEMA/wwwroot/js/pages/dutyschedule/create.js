const shiftList = (window.DutyScheduleConfig && window.DutyScheduleConfig.shiftList) ? window.DutyScheduleConfig.shiftList : [];

function getVietnameseDay(dayIndex) {
    const days = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
    return days[dayIndex];
}

function generateTable() {
    const monthInput = document.getElementById('MonthYear');
    if (!monthInput) return;
    const monthVal = monthInput.value;
    if (!monthVal) return;

    const parts = monthVal.split('-');
    const year = parseInt(parts[0]);
    const month = parseInt(parts[1]);

    const daysInMonth = new Date(year, month, 0).getDate();
    const tbody = document.getElementById('scheduleBody');
    if (!tbody) return;

    tbody.innerHTML = '';

    for (let d = 1; d <= daysInMonth; d++) {
        const dateObj = new Date(year, month - 1, d);
        const dayOfWeek = dateObj.getDay();

        const dateString = year + '-' + String(month).padStart(2, '0') + '-' + String(d).padStart(2, '0');

        const tr = document.createElement('tr');

        if (dayOfWeek === 0 || dayOfWeek === 6) {
            tr.classList.add('table-secondary');
        }

        const tdDate = document.createElement('td');
        tdDate.className = 'align-middle';
        tdDate.innerHTML = `<span class="${dayOfWeek === 0 ? 'text-danger fw-bold' : 'fw-bold'}">${getVietnameseDay(dayOfWeek)}</span>, ${String(d).padStart(2, '0')}/${String(month).padStart(2, '0')}`;
        tr.appendChild(tdDate);

        shiftList.forEach(shift => {
            const td = document.createElement('td');
            td.className = 'text-center align-middle';

            const checkboxHtml = `
                <input type="checkbox" class="form-check-input matrix-checkbox"
                       name="SelectedSchedules"
                       value="${dateString}_${shift.id}"
                       style="width: 20px; height: 20px; cursor: pointer;">
            `;
            td.innerHTML = checkboxHtml;
            tr.appendChild(td);
        });

        tbody.appendChild(tr);
    }
}

let isAllChecked = false;
function checkAll() {
    isAllChecked = !isAllChecked;
    const checkboxes = document.querySelectorAll('.matrix-checkbox');
    checkboxes.forEach(cb => {
        cb.checked = isAllChecked;
    });
}

document.addEventListener("DOMContentLoaded", function() {
    generateTable();
});
