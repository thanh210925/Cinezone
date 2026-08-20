// Matrix functions
function selectMatrixDate(dateStr) {
    document.querySelectorAll('.matrix-date-pill').forEach(pill => {
        if (pill.getAttribute('data-date') === dateStr) {
            pill.classList.add('active');
        } else {
            pill.classList.remove('active');
        }
    });

    const picker = document.getElementById('matrixDatePicker');
    if (picker) {
        picker.value = dateStr === 'all' ? '' : dateStr;
    }

    let foundAny = false;
    document.querySelectorAll('.matrix-date-container').forEach(container => {
        const containerDate = container.getAttribute('data-date');
        if (dateStr === 'all' || containerDate === dateStr) {
            container.classList.remove('d-none');
            foundAny = true;
        } else {
            container.classList.add('d-none');
        }
    });

    const warning = document.getElementById('matrixEmptyWarning');
    if (warning) {
        if (foundAny) {
            warning.classList.add('d-none');
        } else {
            warning.classList.remove('d-none');
        }
    }
}

function onMatrixDatePickerChange(input) {
    const val = input ? input.value : '';
    if (!val) {
        selectMatrixDate('all');
    } else {
        selectMatrixDate(val);
    }
}

// List functions
function toggleRowSelect(event, row) {
    if (event.target.tagName === 'A' || event.target.tagName === 'BUTTON' || event.target.tagName === 'I' || event.target.tagName === 'INPUT' || event.target.closest('form')) {
        return;
    }
    const cb = row.querySelector('.showtime-select-cb');
    if (cb) {
        cb.checked = !cb.checked;
        row.classList.toggle('table-active', cb.checked);
        updateListBulkBar();
    }
}

function toggleAllList(masterCb) {
    const checkboxes = document.querySelectorAll('.showtime-select-cb');
    checkboxes.forEach(cb => {
        cb.checked = masterCb.checked;
        const row = cb.closest('tr');
        if (row) {
            row.classList.toggle('table-active', masterCb.checked);
        }
    });
    updateListBulkBar();
}

function updateListBulkBar() {
    const checkedCbs = document.querySelectorAll('.showtime-select-cb:checked');
    const bar = document.getElementById('listBulkBar');
    const countBadge = document.getElementById('listBulkCount');
    
    if (bar && countBadge) {
        if (checkedCbs.length > 0) {
            countBadge.textContent = checkedCbs.length;
            bar.classList.remove('d-none');
            bar.classList.add('d-flex');
        } else {
            bar.classList.add('d-none');
            bar.classList.remove('d-flex');
        }
    }
    
    const totalCbs = document.querySelectorAll('.showtime-select-cb');
    const masterCb = document.querySelector('.select-all-list-cb');
    if (masterCb) {
        masterCb.checked = totalCbs.length > 0 && checkedCbs.length === totalCbs.length;
    }
}

function triggerListBulkAction(actionName) {
    const checkedCbs = document.querySelectorAll('.showtime-select-cb:checked');
    if (checkedCbs.length === 0) return;

    let confirmMsg = '';
    if (actionName === 'BulkDelete') {
        confirmMsg = `Bạn có chắc chắn muốn xóa ${checkedCbs.length} suất chiếu đã chọn? Thao tác này cũng sẽ xóa vé đã đặt liên quan!`;
    } else if (actionName === 'BulkDeactivate') {
        confirmMsg = `Tạm ngưng hoạt động ${checkedCbs.length} suất chiếu đã chọn?`;
    } else {
        confirmMsg = `Kích hoạt hoạt động ${checkedCbs.length} suất chiếu đã chọn?`;
    }

    if (!confirm(confirmMsg)) return;

    const ids = Array.from(checkedCbs).map(cb => parseInt(cb.value));
    
    fetch(`/Showtime/${actionName}`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(ids)
    })
    .then(response => response.json())
    .then(res => {
        if (res.success) {
            alert(res.message);
            window.location.reload();
        } else {
            alert(res.message);
        }
    })
    .catch(err => {
        alert('Lỗi kết nối máy chủ.');
    });
}
