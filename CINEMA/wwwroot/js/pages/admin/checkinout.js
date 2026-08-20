const video = document.getElementById('video');
const status = document.getElementById('status');
const canvasOverlay = document.getElementById('overlay');
const aiStatusMsg = document.getElementById('ai-status-msg');

let isFaceMatched = false;
let isModelLoaded = false;
let refDescriptor = null;
let faceDetectionInterval = null;
let localStream = null;

// Bắt đầu Camera
if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
    navigator.mediaDevices.getUserMedia({ video: { width: 400, height: 300 } })
        .then(stream => { 
            localStream = stream;
            video.srcObject = stream;
            initFaceApi();
        })
        .catch(err => { 
            if (status) {
                status.innerText = "Lỗi Camera!"; 
                status.style.color = "red"; 
            }
        });
}

async function initFaceApi() {
    const registeredFace = window.CheckInOutConfig ? window.CheckInOutConfig.registeredFace : '';

    if (!registeredFace) {
        if (aiStatusMsg) {
            aiStatusMsg.className = "alert alert-warning py-2 mb-2";
            aiStatusMsg.innerHTML = `<i class="bi bi-exclamation-triangle-fill"></i> Bạn chưa đăng ký khuôn mặt! Chấm công sẽ ở trạng thái <b>Chờ duyệt</b>.`;
            aiStatusMsg.style.display = 'block';
        }
        return;
    }

    try {
        if (aiStatusMsg) {
            aiStatusMsg.className = "alert alert-info py-2 mb-2";
            aiStatusMsg.innerHTML = `<span class="spinner-border spinner-border-sm me-2"></span> Đang tải mô hình nhận diện khuôn mặt AI...`;
            aiStatusMsg.style.display = 'block';
        }

        const MODEL_URL = (window.CheckInOutConfig && window.CheckInOutConfig.modelUrl) ? window.CheckInOutConfig.modelUrl : '/models/';
        await faceapi.nets.ssdMobilenetv1.loadFromUri(MODEL_URL);
        await faceapi.nets.faceLandmark68Net.loadFromUri(MODEL_URL);
        await faceapi.nets.faceRecognitionNet.loadFromUri(MODEL_URL);
        isModelLoaded = true;

        const refImg = await faceapi.fetchImage(registeredFace);
        const refDetection = await faceapi.detectSingleFace(refImg).withFaceLandmarks().withFaceDescriptor();
        
        if (!refDetection) {
            if (aiStatusMsg) {
                aiStatusMsg.className = "alert alert-danger py-2 mb-2";
                aiStatusMsg.innerHTML = `<i class="bi bi-x-circle-fill"></i> Ảnh mẫu đăng ký không hợp lệ (không tìm thấy khuôn mặt).`;
            }
            return;
        }
        
        refDescriptor = refDetection.descriptor;
        if (aiStatusMsg) {
            aiStatusMsg.className = "alert alert-success py-2 mb-2";
            aiStatusMsg.innerHTML = `<i class="bi bi-check-circle-fill"></i> Mô hình AI sẵn sàng. Đang quét khuôn mặt...`;
        }

        startTrackingFace();
    } catch (ex) {
        console.error("Lỗi khởi tạo AI:", ex);
        if (aiStatusMsg) {
            aiStatusMsg.className = "alert alert-danger py-2 mb-2";
            aiStatusMsg.innerHTML = `<i class="bi bi-exclamation-octagon-fill"></i> Lỗi hệ thống AI: ${ex.message}`;
        }
    }
}

const btnCheckIn = document.getElementById('btnCheckIn');
if (btnCheckIn) {
    btnCheckIn.addEventListener('click', async () => {
        const registeredDescriptor = (window.CheckInOutConfig && window.CheckInOutConfig.faceDescriptorJson) ? JSON.parse(window.CheckInOutConfig.faceDescriptorJson) : null; 

        const result = await verifyFace(video, registeredDescriptor);

        if (result && result.match) {
            alert("Khuôn mặt khớp! Đang thực hiện chấm công...");
            sendAttendanceToServer('in'); 
        } else {
            alert("Khuôn mặt không khớp! Vui lòng thử lại.");
        }
    });
}

function startTrackingFace() {
    const runTracking = () => {
        const width = video.videoWidth || video.clientWidth || 400;
        const height = video.videoHeight || video.clientHeight || 300;
        const displaySize = { width: width, height: height };
        if (canvasOverlay) faceapi.matchDimensions(canvasOverlay, displaySize);

        if (faceDetectionInterval) clearInterval(faceDetectionInterval);

        faceDetectionInterval = setInterval(async () => {
            if (video.paused || video.ended || !refDescriptor) return;

            try {
                const mirrorCanvas = document.createElement('canvas');
                mirrorCanvas.width = width;
                mirrorCanvas.height = height;
                const mCtx = mirrorCanvas.getContext('2d');
                mCtx.translate(width, 0);
                mCtx.scale(-1, 1);
                mCtx.drawImage(video, 0, 0, width, height);

                const detection = await faceapi.detectSingleFace(mirrorCanvas, new faceapi.SsdMobilenetv1Options({ minConfidence: 0.5 }))
                                             .withFaceLandmarks()
                                             .withFaceDescriptor();

                if (canvasOverlay) {
                    const context = canvasOverlay.getContext('2d');
                    context.clearRect(0, 0, context.canvas.width, context.canvas.height);

                    if (detection) {
                        const resizedDetections = faceapi.resizeResults([detection], displaySize);
                        faceapi.draw.drawDetections(canvasOverlay, resizedDetections);

                        const distance = faceapi.euclideanDistance(detection.descriptor, refDescriptor);
                        const threshold = 0.45;

                        if (distance < threshold) {
                            if (status) status.innerHTML = `<span class="text-success"><i class="bi bi-patch-check-fill"></i> Đã nhận diện đúng khuôn mặt! (Độ sai lệch: ${distance.toFixed(3)})</span>`;
                            isFaceMatched = true;
                            if (aiStatusMsg) {
                                aiStatusMsg.className = "alert alert-success py-2 mb-2";
                                aiStatusMsg.innerHTML = `<i class="bi bi-check-circle-fill"></i> Khuôn mặt khớp (${distance.toFixed(3)}). Chấm công sẽ được <b>tự động duyệt</b>.`;
                            }
                        } else {
                            if (status) status.innerHTML = `<span class="text-danger"><i class="bi bi-patch-exclamation-fill"></i> Khuôn mặt không khớp! (Độ sai lệch: ${distance.toFixed(3)})</span>`;
                            isFaceMatched = false;
                            if (aiStatusMsg) {
                                aiStatusMsg.className = "alert alert-danger py-2 mb-2";
                                aiStatusMsg.innerHTML = `<i class="bi bi-exclamation-triangle-fill"></i> Khuôn mặt không khớp. Chấm công sẽ <b>chờ Admin duyệt thủ công</b>.`;
                            }
                        }
                    } else {
                        if (status) status.innerHTML = `<span class="text-warning"><i class="bi bi-person-bounding-box"></i> Đang tìm khuôn mặt...</span>`;
                        isFaceMatched = false;
                    }
                }
            } catch (e) {
                console.error("Lỗi trong vòng quét so khớp:", e);
                isFaceMatched = false;
            }
        }, 500);
    };

    if (video.readyState >= 2 || !video.paused) {
        runTracking();
    } else {
        video.addEventListener('play', runTracking, { once: true });
    }
}

function takeSnapshot(type) {
    const hasRegisteredFace = (window.CheckInOutConfig && window.CheckInOutConfig.registeredFace) ? (window.CheckInOutConfig.registeredFace !== '') : false;

    if (hasRegisteredFace && !isFaceMatched) {
        alert('⚠️ Khuôn mặt không khớp với khuôn mặt đã đăng ký. Không thể chấm công!');
        return;
    }

    if (status) status.innerText = "Đang xử lý...";
    let canvasTemp = document.getElementById('canvas');
    if (canvasTemp) {
        canvasTemp.getContext('2d').drawImage(video, 0, 0, 400, 300);
        let imageData = canvasTemp.toDataURL('image/png');

        fetch('/Admin/ProcessCheck', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ type: type, imageBase64: imageData, isFaceMatched: isFaceMatched })
        })
        .then(res => res.json())
        .then(data => {
            if(data.success) {
                alert(data.message);
                location.reload();
            }
            else alert(data.message);
        });
    }
}

function selectDate(dateString) {
    window.location.href = '/Admin/CheckInOut?date=' + dateString;
}

function approveAttendance(id) {
    fetch('/Admin/ApproveAttendance/' + id, { method: 'POST' })
    .then(res => res.json())
    .then(data => { if(data.success) location.reload(); else alert(data.message); });
}

function approveAll() {
    let date = (window.CheckInOutConfig && window.CheckInOutConfig.selectedDate) ? window.CheckInOutConfig.selectedDate : '';
    fetch('/Admin/ApproveAll?date=' + date, { method: 'POST' })
    .then(res => res.json())
    .then(data => { if(data.success) location.reload(); else alert("Lỗi!"); });
}
