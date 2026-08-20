document.addEventListener("DOMContentLoaded", function () {
    const unlockModal = document.getElementById('unlockModal');
    if (unlockModal) {
        unlockModal.addEventListener('show.bs.modal', function (event) {
            const button = event.relatedTarget;
            if (!button) return;
            const payrollId = button.getAttribute('data-id');
            const employeeName = button.getAttribute('data-name');
            
            const modalPayrollId = unlockModal.querySelector('#unlockPayrollId');
            const modalEmployeeName = unlockModal.querySelector('#unlockEmployeeName');
            const passwordInput = unlockModal.querySelector('#confirmPassword');
            
            if (modalPayrollId) modalPayrollId.value = payrollId;
            if (modalEmployeeName) modalEmployeeName.textContent = employeeName;
            if (passwordInput) passwordInput.value = '';
        });
        
        unlockModal.addEventListener('shown.bs.modal', function () {
            const passwordInput = unlockModal.querySelector('#confirmPassword');
            if (passwordInput) passwordInput.focus();
        });
    }

    const btnDetails = document.querySelectorAll('.btn-detail');
    btnDetails.forEach(btn => {
        btn.addEventListener('click', function() {
            const name = this.getAttribute('data-name');
            const code = this.getAttribute('data-code');
            const position = this.getAttribute('data-position');
            const coef = parseFloat(this.getAttribute('data-coef')) || 0;
            const workhours = parseFloat(this.getAttribute('data-workhours')) || 0;
            const leavehours = parseFloat(this.getAttribute('data-leavehours')) || 0;
            const basesalary = parseFloat(this.getAttribute('data-basesalary')) || 0;
            const bonus = parseFloat(this.getAttribute('data-bonus')) || 0;
            const deduct = parseFloat(this.getAttribute('data-deduct')) || 0;
            const totalsalary = parseFloat(this.getAttribute('data-totalsalary')) || 0;
            const notes = this.getAttribute('data-notes') || 'Không có ghi chú';
            const status = this.getAttribute('data-status');
            const month = this.getAttribute('data-month');
            const year = this.getAttribute('data-year');

            const empNameEl = document.getElementById('detailEmpName');
            if (empNameEl) empNameEl.textContent = name;
            const empCodeEl = document.getElementById('detailEmpCode');
            if (empCodeEl) empCodeEl.textContent = 'MSNV: ' + code;
            const empPosEl = document.getElementById('detailEmpPosition');
            if (empPosEl) empPosEl.textContent = position;
            const timeEl = document.getElementById('detailTime');
            if (timeEl) timeEl.textContent = 'Tháng ' + month + '/' + year;

            const workHoursEl = document.getElementById('detailWorkingHours');
            if (workHoursEl) workHoursEl.textContent = workhours.toFixed(2) + ' giờ';
            const leaveHoursEl = document.getElementById('detailLeaveHours');
            if (leaveHoursEl) leaveHoursEl.textContent = leavehours.toFixed(2) + ' giờ';
            const baseSalaryEl = document.getElementById('detailBaseSalary');
            if (baseSalaryEl) baseSalaryEl.textContent = basesalary.toLocaleString('vi-VN') + ' đ';
            const coefEl = document.getElementById('detailCoef');
            if (coefEl) coefEl.textContent = coef.toFixed(2);

            const jobSalary = (workhours + leavehours) * basesalary * coef;
            const jobSalaryEl = document.getElementById('detailJobSalary');
            if (jobSalaryEl) jobSalaryEl.textContent = jobSalary.toLocaleString('vi-VN') + ' đ';

            const bonusEl = document.getElementById('detailBonus');
            if (bonusEl) bonusEl.textContent = '+' + bonus.toLocaleString('vi-VN') + ' đ';
            const deductEl = document.getElementById('detailDeductions');
            if (deductEl) deductEl.textContent = '-' + deduct.toLocaleString('vi-VN') + ' đ';
            const totalSalaryEl = document.getElementById('detailTotalSalary');
            if (totalSalaryEl) totalSalaryEl.textContent = totalsalary.toLocaleString('vi-VN') + ' đ';

            const notesEl = document.getElementById('detailNotes');
            if (notesEl) notesEl.textContent = notes;

            const statusEl = document.getElementById('detailStatus');
            if (statusEl) {
                statusEl.textContent = status;
                if (status === 'Đã thanh toán') {
                    statusEl.className = 'badge bg-success';
                } else {
                    statusEl.className = 'badge bg-warning text-dark';
                }
            }

            const modalEl = document.getElementById('detailModal');
            if (modalEl && window.bootstrap && window.bootstrap.Modal) {
                const bsModal = new bootstrap.Modal(modalEl);
                bsModal.show();
            }
        });
    });
});
