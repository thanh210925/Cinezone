function openReplyModal(id, customerName, comment) {
    const reviewIdInput = document.getElementById("replyReviewId");
    if (reviewIdInput) reviewIdInput.value = id;
    
    const custCommentText = document.getElementById("customerCommentText");
    if (custCommentText) custCommentText.innerText = `"${customerName}": ${comment}`;
    const replyText = document.getElementById("replyText");
    if (replyText) replyText.value = "";
    
    const modalEl = document.getElementById('replyModal');
    if (modalEl && window.bootstrap && window.bootstrap.Modal) {
        var modal = new bootstrap.Modal(modalEl);
        modal.show();
    }
}

document.addEventListener("DOMContentLoaded", function () {
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[title]'));
    if (window.bootstrap && window.bootstrap.Tooltip) {
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }
});
