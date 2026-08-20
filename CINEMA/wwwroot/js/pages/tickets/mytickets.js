function startCountdown() {
    const countdowns = document.querySelectorAll(".countdown");

    countdowns.forEach(el => {
        const expiredAttr = el.getAttribute("data-expired");
        if (!expiredAttr) return;
        const expiredTime = new Date(expiredAttr).getTime();

        const interval = setInterval(() => {
            const now = new Date().getTime();
            const distance = expiredTime - now;

            if (distance <= 0) {
                el.innerHTML = "Hết hạn";

                const card = el.closest(".card");
                if (card) {
                    const btn = card.querySelector(".pay-btn");
                    if (btn) {
                        btn.classList.add("disabled");
                        btn.innerText = "⛔ Hết hạn";
                        btn.style.pointerEvents = "none";
                    }
                }

                clearInterval(interval);
                return;
            }

            const minutes = Math.floor(distance / (1000 * 60));
            const seconds = Math.floor((distance % (1000 * 60)) / 1000);

            el.innerHTML = minutes + "m " + seconds + "s";
        }, 1000);
    });
}

document.addEventListener("DOMContentLoaded", function () {
    startCountdown();
});
