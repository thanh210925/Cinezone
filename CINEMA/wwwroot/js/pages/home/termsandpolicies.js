function activateLink(element) {
    document.querySelectorAll(".policy-menu-link").forEach(link => {
        link.classList.remove("active");
    });
    if (element) element.classList.add("active");
}
