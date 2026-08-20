let currentAdminId = null;
let localStream = null;
let faceDetectionInterval = null;
let isModelLoaded = false;

const video = document.getElementById('webcam');
const canvas = document.getElementById('overlay');
const btnCapture = document.getElementById('btnCapture');
const cameraStatus = document.getElementById('cameraStatus');
const aiLoading = document.getElementById('ai-loading');

// Hàm mở modal và kích hoạt Camera
async function openRegisterModal(adminId, fullName) {
    currentAdminId = adminId;
    const targetNameEl = document.getElementById('targetAdminName');
    if (targetNameEl) targetNameEl.innerText = fullName;
    
    const modalEl = document.getElementById('registerFaceModal');
    if (modalEl) {
        const myModal = new bootstrap.Modal(modalEl);
        myModal.show();
    }

    await initCamera();
}

// Khởi động Camera và mô hình face-api
async function initCamera() {
    try {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) return;
        localStream = await navigator.mediaDevices.getUserMedia({ video: { width: 400, height: 300 } });
        if (video) video.srcObject = localStream;

        if (!isModelLoaded) {
            if (aiLoading) aiLoading.style.display = 'block';
            const MODEL_URL = (window.RegisterFaceConfig && window.RegisterFaceConfig.modelUrl) ? window.RegisterFaceConfig.modelUrl : '/models/';
            await faceapi.nets.ssdMobilenetv1.loadFromUri(MODEL_URL);
            await faceapi.nets.faceLandmark68Net.loadFromUri(MODEL_URL);
            await faceapi.nets.faceRecognitionNet.loadFromUri(MODEL_URL);
            isModelLoaded = true;
        }
        
        if (aiLoading) aiLoading.style.display = 'none';
        startFaceDetection();
    } catch (err) {
        console.error("Lỗi Camera hoặc Tải mô hình AI:", err);
        if (cameraStatus) {
            cameraStatus.className = "alert alert-danger py-2 mb-3";
            cameraStatus.innerText = "Lỗi: " + (err.message || err) + " (Vui lòng dừng debug Visual Studio và chạy lại nếu vừa cập nhật code)";
        }
        if (aiLoading) aiLoading.style.display = 'none';
    }
}

// Bật tắt chế độ chụp thủ công
function toggleManualMode(checkbox) {
    if (checkbox.checked) {
        if (btnCapture) btnCapture.removeAttribute('disabled');
        if (cameraStatus) {
            cameraStatus.className = "alert alert-info py-2 mb-3";
            cameraStatus.innerHTML = `<i class="bi bi-info-circle-fill"></i> Chế độ chụp thủ công hoạt động. Nhấn nút để chụp.`;
        }
    } else {
        if (btnCapture) btnCapture.setAttribute('disabled', 'true');
        if (cameraStatus) {
            cameraStatus.className = "alert alert-warning py-2 mb-3";
            cameraStatus.innerText = "Vui lòng hướng khuôn mặt trực diện vào camera";
        }
    }
}

// Bắt đầu nhận diện khuôn mặt liên tục để kiểm duyệt
function startFaceDetection() {
    const runDetection = () => {
        if (!video) return;
        const width = video.videoWidth || video.clientWidth || 400;
        const height = video.videoHeight || video.clientHeight || 300;
        const displaySize = { width: width, height: height };
        if (canvas) faceapi.matchDimensions(canvas, displaySize);

        if (faceDetectionInterval) clearInterval(faceDetectionInterval);

        faceDetectionInterval = setInterval(async () => {
            if (video.paused || video.ended) return;

            try {
                const detections = await faceapi.detectAllFaces(video, new faceapi.SsdMobilenetv1Options({ minConfidence: 0.4 }))
                                          .withFaceLandmarks();

                const resizedDetections = faceapi.resizeResults(detections, displaySize);
                
                if (canvas) {
                    const context = canvas.getContext('2d');
                    context.clearRect(0, 0, context.canvas.width, context.canvas.height);
                    
                    const chkManual = document.getElementById('chkManual');
                    if (chkManual && !chkManual.checked) {
                        faceapi.draw.drawDetections(canvas, resizedDetections);

                        if (detections.length === 1) {
                            if (cameraStatus) {
                                cameraStatus.className = "alert alert-success py-2 mb-3";
                                cameraStatus.innerHTML = `<i class="bi bi-check-circle-fill"></i> Phát hiện 1 khuôn mặt hợp lệ! Có thể chụp.`;
                            }
                            if (btnCapture) btnCapture.removeAttribute('disabled');
                        } else if (detections.length > 1) {
                            if (cameraStatus) {
                                cameraStatus.className = "alert alert-warning py-2 mb-3";
                                cameraStatus.innerHTML = `<i class="bi bi-exclamation-triangle-fill"></i> Có nhiều hơn 1 khuôn mặt trong khung hình.`;
                            }
                            if (btnCapture) btnCapture.setAttribute('disabled', 'true');
                        } else {
                            if (cameraStatus) {
                                cameraStatus.className = "alert alert-warning py-2 mb-3";
                                cameraStatus.innerHTML = `<i class="bi bi-person-bounding-box"></i> Đang quét khuôn mặt... Vui lòng đứng trước camera.`;
                            }
                            if (btnCapture) btnCapture.setAttribute('disabled', 'true');
                        }
                    }
                }
            } catch (e) {
                console.error("Lỗi trong vòng lặp AI:", e);
            }
        }, 500);
    };

    if (video.readyState >= 2 || !video.paused) {
        runDetection();
    } else {
        video.addEventListener('play', runDetection, { once: true });
    }
}

// Tắt camera và dọn dẹp
function stopCamera() {
    if (faceDetectionInterval) {
        clearInterval(faceDetectionInterval);
        faceDetectionInterval = null;
    }
    if (localStream) {
        localStream.getTracks().forEach(track => track.stop());
        localStream = null;
    }
    if (video) video.srcObject = null;
    if (canvas) {
        const context = canvas.getContext('2d');
        context.clearRect(0, 0, context.canvas.width, context.canvas.height);
    }
    if (btnCapture) btnCapture.setAttribute('disabled', 'true');
    const chkManual = document.getElementById('chkManual');
    if (chkManual) chkManual.checked = false;
    if (cameraStatus) {
        cameraStatus.className = "alert alert-warning py-2 mb-3";
        cameraStatus.innerText = "Vui lòng hướng khuôn mặt trực diện vào camera";
    }
}

// Chụp ảnh, chuyển thành Base64 và gọi API lưu
async function captureAndSave() {
    if (btnCapture) btnCapture.setAttribute('disabled', 'true');
    if (cameraStatus) {
        cameraStatus.className = "alert alert-info py-2 mb-3";
        cameraStatus.innerText = "Đang lưu trữ khuôn mặt lên hệ thống...";
    }

    const tempCanvas = document.createElement('canvas');
    tempCanvas.width = 400;
    tempCanvas.height = 300;
    const ctx = tempCanvas.getContext('2d');
    
    ctx.translate(tempCanvas.width, 0);
    ctx.scale(-1, 1);
    ctx.drawImage(video, 0, 0, tempCanvas.width, tempCanvas.height);
    
    const base64Data = tempCanvas.toDataURL('image/png');

    try {
        const response = await fetch('/Admin/SaveFace', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ AdminId: currentAdminId, imageBase64: base64Data })
        });
        const result = await response.json();

        if (result.success) {
            alert("Đăng ký khuôn mặt thành công!");
            stopCamera();
            const modalEl = document.getElementById('registerFaceModal');
            if (modalEl) {
                const modal = bootstrap.Modal.getInstance(modalEl);
                if (modal) modal.hide();
            }
            location.reload();
        } else {
            alert("Lỗi: " + result.message);
            if (btnCapture) btnCapture.removeAttribute('disabled');
            if (cameraStatus) {
                cameraStatus.className = "alert alert-danger py-2 mb-3";
                cameraStatus.innerText = "Lỗi khi lưu khuôn mặt!";
            }
        }
    } catch (ex) {
        console.error(ex);
        alert("Lỗi mạng khi kết nối server!");
        if (btnCapture) btnCapture.removeAttribute('disabled');
    }
}

// Xóa khuôn mặt
async function deleteFace(adminId, fullName) {
    if (!confirm(`Bạn có chắc chắn muốn xóa khuôn mặt đã đăng ký của nhân viên ${fullName} không?`)) {
        return;
    }

    try {
        const response = await fetch(`/Admin/DeleteFace?adminId=${adminId}`, {
            method: 'POST'
        });
        const result = await response.json();

        if (result.success) {
            alert("Xóa khuôn mặt thành công!");
            location.reload();
        } else {
            alert("Lỗi: " + result.message);
        }
    } catch (ex) {
        console.error(ex);
        alert("Lỗi mạng khi kết nối server!");
    }
}
