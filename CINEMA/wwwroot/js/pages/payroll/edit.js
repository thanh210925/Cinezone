document.addEventListener("DOMContentLoaded", function () {
    const workingHoursInput = document.getElementById("WorkingHours");
    const paidLeaveHoursInput = document.getElementById("PaidLeaveHours");
    const baseSalaryInput = document.getElementById("BaseSalaryPerHour");
    const coefficientInput = document.getElementById("SalaryCoefficient");
    const bonusInput = document.getElementById("Bonus");
    const deductionsInput = document.getElementById("Deductions");
    const totalSalaryInput = document.getElementById("totalSalaryInput");
    const totalSalaryDisplay = document.getElementById("totalSalaryDisplay");

    function calculateSalary() {
        if (!workingHoursInput || !paidLeaveHoursInput || !baseSalaryInput || !coefficientInput || !bonusInput || !deductionsInput) return;

        const workingHours = parseFloat(workingHoursInput.value) || 0;
        const paidLeaveHours = parseFloat(paidLeaveHoursInput.value) || 0;
        const baseSalary = parseFloat(baseSalaryInput.value) || 0;
        const coefficient = parseFloat(coefficientInput.value) || 0;
        const bonus = parseFloat(bonusInput.value) || 0;
        const deductions = parseFloat(deductionsInput.value) || 0;

        let totalSalary = ((workingHours + paidLeaveHours) * baseSalary * coefficient) + bonus - deductions;
        if (totalSalary < 0) totalSalary = 0;

        if (totalSalaryInput) totalSalaryInput.value = totalSalary.toFixed(0);
        if (totalSalaryDisplay) totalSalaryDisplay.textContent = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(totalSalary);
    }

    const triggers = document.querySelectorAll(".calc-trigger");
    triggers.forEach(trigger => {
        trigger.addEventListener("input", calculateSalary);
    });

    calculateSalary();
});
