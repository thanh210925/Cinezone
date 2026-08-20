let movieIndex = 0;

function addMovie() {
    let html = `
        <div class="card p-4 mb-4 movie-row shadow-sm border-0 position-relative">
            <button type="button" class="btn-close position-absolute top-0 end-0 m-3" onclick="this.parentElement.remove()"></button>
            <div class="row g-3">
                <div class="col-12">
                    <label class="fw-bold small text-muted">Tên phim</label>
                    <input name="Movies[${movieIndex}].Title" class="form-control" placeholder="Tên phim..." required>
                </div>

                <div class="col-md-4">
                    <label class="fw-bold small text-muted">Thời lượng (phút)</label>
                    <input name="Movies[${movieIndex}].Duration" type="number" class="form-control" placeholder="120">
                </div>
                <div class="col-md-4">
                    <label class="fw-bold small text-muted">Ngày khởi chiếu</label>
                    <input name="Movies[${movieIndex}].ReleaseDate" type="date" class="form-control">
                </div>
                <div class="col-md-4">
                    <label class="fw-bold small text-muted">Ngày kết thúc chiếu</label>
                    <input name="Movies[${movieIndex}].EndDate" type="date" class="form-control">
                </div>

                <div class="col-md-4">
                    <input name="Movies[${movieIndex}].Country" class="form-control" placeholder="Quốc gia">
                </div>
                <div class="col-md-4">
                    <input name="Movies[${movieIndex}].Language" class="form-control" placeholder="Ngôn ngữ">
                </div>
                <div class="col-md-4">
                    <input name="Movies[${movieIndex}].AgeRating" class="form-control" placeholder="Tuổi (VD: 13+)">
                </div>

                <div class="col-12">
                    <textarea name="Movies[${movieIndex}].Description" class="form-control" rows="2" placeholder="Mô tả chi tiết..."></textarea>
                </div>

                <div class="col-md-6">
                    <input name="Movies[${movieIndex}].PosterUrl" class="form-control" placeholder="Poster URL">
                </div>
                <div class="col-md-6">
                    <input name="Movies[${movieIndex}].TrailerUrl" class="form-control" placeholder="Trailer URL (YouTube/MP4)">
                </div>
            </div>
        </div>`;

    const container = document.getElementById("movieContainer");
    if (container) container.insertAdjacentHTML("beforeend", html);
    movieIndex++;
}

document.addEventListener("DOMContentLoaded", function () {
    addMovie();
});
