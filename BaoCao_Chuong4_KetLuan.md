# CHƯƠNG 4: KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN

---

## 4.1. Kết quả đạt được

Sau quá trình nghiên cứu, phân tích thiết kế và triển khai thực nghiệm, dự án **Hệ thống thương mại điện tử đặt vé xem phim trực tuyến Cinezone** đã hoàn thành các mục tiêu đề ra, xây dựng thành công một nền tảng thương mại điện tử hiện đại, toàn diện và có tính ứng dụng cao.

Các kết quả cụ thể đạt được bao gồm:

### 1. Phân hệ dành cho Khách hàng (Client Web Portal)
- **Đăng ký & Xác thực an toàn:** Hỗ trợ đăng ký/đăng nhập tài khoản truyền thống (mật khẩu được mã hóa an toàn bằng thuật toán **BCrypt**) và đăng nhập nhanh qua **Google OAuth 2.0**.
- **Tra cứu & Tìm kiếm phim thông minh:** Cho phép khách hàng tìm kiếm phim theo thể loại, cụm rạp, trạng thái (Phim đang chiếu / Phim sắp chiếu) và xem trailer, thông tin chi tiết.
- **Đặt vé & Chọn ghế thời gian thực (Real-time Seat Booking):**
  - Giao diện sơ đồ phòng chiếu trực quan, tương tác mượt mà.
  - Tích hợp công nghệ **ASP.NET Core SignalR** giúp cập nhật và đồng bộ trạng thái giữ ghế/chọn ghế theo thời gian thực giữa nhiều người dùng cùng lúc, tránh xung đột đặt trùng ghế.
  - Tính năng **Đặt vé nhóm (Group Booking - `GroupBookingHub`)** cho phép rủ bạn bè cùng tham gia chọn ghế và hoàn tất đơn hàng chung.
- **Dịch vụ đi kèm & Khuyến mãi linh hoạt:**
  - Cho phép chọn mua kèm các gói Combo bắp nước và đồ uống.
  - Hệ thống áp dụng mã giảm giá động (**Voucher Rule Engine**), tự động kiểm tra các điều kiện phức tạp (số lượng vé tối thiểu, mua kèm combo, loại tài khoản, giá trị đơn hàng) trước khi giảm giá.
- **Thanh toán trực tuyến đa kênh:** Tích hợp thành công 2 cổng thanh toán điện tử uy tín là **VNPay** và **Stripe**, cho phép thanh toán qua thẻ ATM nội địa, QR Code ngân hàng và thẻ quốc tế (Visa/Mastercard).
- **Vé điện tử & Thông báo:** Tự động tạo và gửi vé điện tử (chứa mã tra cứu / QR Code) qua Email (**EmailService**) ngay khi thanh toán thành công.
- **Tương tác & Đánh giá:** Khách hàng có thể gửi bình luận, chấm điểm sao đánh giá các bộ phim đã xem (`ReviewsController`).

### 2. Tích hợp Trí tuệ nhân tạo (AI & Smart Features)
- **Chatbot trợ lý ảo thông minh (`ChatbotController`):** Tích hợp **Google Gemini AI API** để tư vấn lịch chiếu, giải đáp thắc mắc dịch vụ và hỗ trợ khách hàng tự động 24/7.
- **Hệ thống Gợi ý Phim cá nhân hóa (`RecommendationEngine`):** Phân tích hành vi xem phim, lịch sử đặt vé và sở thích của từng khách hàng để đưa ra danh sách gợi ý phim phù hợp nhất.
- **Phân nhóm khách hàng tự động (`CustomerClusteringService`):** Áp dụng thuật toán phân cụm khách hàng (dựa trên tần suất đặt vé, tổng chi tiêu, thói quen xem phim) hỗ trợ bộ phận Marketing đưa ra các chiến dịch ưu đãi nhắm trúng mục tiêu.

### 3. Phân hệ Quản trị & Vận hành (Admin & Staff Management)
- **Quản lý Danh mục & Rạp chiếu:** Quản lý toàn bộ hệ thống Cụm rạp (Branch), Phòng chiếu (Auditorium), Sơ đồ ghế (Seats), Phim (Movie), Thể loại (Genre) và Lịch chiếu (Showtimes).
- **Quản lý Khuyến mãi & Điều kiện Voucher:** Cho phép Admin cấu hình linh hoạt các điều kiện giảm giá (VoucherConditions & Rules) mà không cần can thiệp vào mã nguồn.
- **Quản lý Nhân sự & Phân lịch trực (Duty Schedule & Payroll):**
  - Quản lý danh sách nhân viên, chức vụ (Positions), ca làm việc (Shifts).
  - Phân công lịch trực theo ca cho nhân viên rạp và tự động tính bảng lương (`PayrollController`) dựa trên số ca trực và mức lương cố định/theo giờ.
- **Báo cáo & Thống kê doanh thu trực quan:**
  - Dashboard thống kê doanh thu theo thời gian (ngày/tháng/năm), theo cụm rạp, theo phim và doanh thu bắp nước qua các biểu đồ tương tác (`RevenueDashboardViewComponent`).
  - Hỗ trợ xuất dữ liệu báo cáo ra file **Excel** chuyên nghiệp bằng thư viện **EPPlus**.

### 4. Kiến trúc Hạ tầng & Mã nguồn
- Ứng dụng được phát triển trên nền tảng **ASP.NET Core 8 MVC**, kết hợp **Entity Framework Core 9.0** và **SQL Server**, tuân thủ nghiêm ngặt các nguyên lý thiết kế phần mềm sạch (Clean Code), mô hình MVC và phân quyền bảo mật chặt chẽ (`AdminBaseController`).
- Sẵn sàng cho việc đóng gói **Docker Container** và triển khai theo quy trình **CI/CD (GitHub Actions)** tự động.

---

## 4.2. Những hạn chế

Mặc dù đã đạt được nhiều kết quả tích cực, hệ thống Cinezone vẫn còn tồn tại một số hạn chế cần tiếp tục hoàn thiện trong tương lai:

1. **Thanh toán trực tuyến phụ thuộc môi trường Sandbox:**
   - Các tích hợp cổng thanh toán VNPay và Stripe hiện chủ yếu hoạt động trên môi trường thử nghiệm (Sandbox/Test Key), chưa được kiểm thử giao dịch tài chính thật với hợp đồng merchant pháp lý của ngân hàng thương mại.

2. **Cơ chế Khóa ghế đồng thời (Seat Concurrency Locking) ở quy mô cực lớn:**
   - Việc giữ ghế tạm thời hiện đang dựa trên SignalR WebSocket và Session memory. Khi lưu lượng truy cập đạt ngưỡng hàng trăm ngàn lượt nhấp chọn ghế cùng lúc (Traffic Spike), hệ thống vẫn có nguy cơ nhỏ xảy ra tình trạng xung đột dữ liệu (Race Condition) nếu không có bộ khóa phân tán chuyên dụng (**Distributed Lock** như Redis Redlock).

3. **Thiếu ứng dụng di động độc lập (Native Mobile App):**
   - Hệ thống hiện tại vận hành trên nền tảng Web Responsive (tương thích giao diện trình duyệt di động), chưa có ứng dụng native chạy trên iOS (Swift/SwiftUI) hoặc Android (Kotlin/Flutter) đăng tải trên App Store / Google Play.

4. **Tập dữ liệu huấn luyện cho Mô hình AI còn hạn chế:**
   - Khả năng gợi ý phim (`RecommendationEngine`) và phân nhóm khách hàng (`CustomerClusteringService`) hiện chạy trên tập dữ liệu mô phỏng nội bộ. Để đạt độ chính xác cao hơn, hệ thống cần lượng dữ liệu người dùng thực tế tích lũy qua thời gian dài vận hành.

5. **Chưa tích hợp công cụ quét mã vé tự động tại cửa rạp:**
   - Hiện tại vé điện tử được gửi qua Email, tuy nhiên hệ thống chưa có ứng dụng dành riêng cho nhân viên soát vé (Staff App) để quét mã QR Code/NFC tại cửa phòng chiếu.

---

## 4.3. Định hướng mở rộng và cải tiến hệ thống

Để nâng cao trải nghiệm người dùng, tối ưu hóa quy trình vận hành rạp và mở rộng quy mô kinh doanh, trong thời gian tới hệ thống Cinezone hướng đến các cải tiến trọng tâm sau:

### 1. Nâng cấp Kiến trúc & Công nghệ
- **Chuyển đổi sang Kiến trúc Microservices:** Tách các phân hệ hiện tại thành các dịch vụ độc lập như *Identity Service*, *Booking Service*, *Payment Service*, *Notification Service*, *AI Service* giao tiếp thông qua Message Broker (**RabbitMQ / Apache Kafka**). Điều này giúp hệ thống mở rộng (Scale-out) cực kỳ linh hoạt và tăng khả năng chịu lỗi cách ly.
- **Áp dụng Redis Distributed Lock:** Triển khai Redis Redlock để đảm bảo tính toàn vẹn tuyệt đối cho giao dịch chọn giữ ghế thời gian thực, triệt tiêu 100% nguy cơ trùng ghế khi mở bán vé các phim bom tấn.

### 2. Phát triển Ứng dụng Di động (Mobile Native Apps)
- Xây dựng ứng dụng **Cinezone Mobile App** bằng **Flutter / React Native** hỗ trợ cả iOS và Android.
- Tích hợp tính năng **Push Notification (Firebase Cloud Messaging - FCM)** để tự động gửi thông báo lịch chiếu phim đã đặt, nhắc giờ xem phim trước 30 phút, cũng như các chương trình khuyến mãi cá nhân hóa.

### 3. Số hóa Quy trình Soát vé (Smart Gate Check-in)
- Phát triển ứng dụng di động dành cho Nhân viên soát vé (**Cinezone Staff Scanner App**) tích hợp camera quét mã QR Code trên vé điện tử hoặc công nghệ đọc thẻ **NFC**.
- Kết nối trực tiếp với thiết bị **Cổng soát vé tự động (Flap Barrier / Turnstile)** tại rạp để cho phép khách hàng tự quét mã QR vào phòng chiếu mà không cần nhân viên đứng soát thủ công.

### 4. Tối ưu hóa & Nâng cấp Mô hình Trí tuệ Nhân tạo (Advanced AI/ML)
- Tích hợp mô hình lọc cộng tác (**Collaborative Filtering**) kết hợp Deep Learning để gợi ý phim và suất chiếu thông minh hơn dựa trên thời gian rảnh và vị trí địa lý của khách hàng.
- Nâng cấp **Chatbot AI** thành trợ lý giọng nói (Voice Assistant) cho phép khách hàng đặt vé và tra cứu lịch chiếu bằng giọng nói tiếng Việt.
- Áp dụng AI dự báo doanh thu (Revenue Forecasting) giúp quản trị viên chủ động lập lịch chiếu phim hiệu quả cho từng phòng chiếu.

### 5. Mở rộng Cổng thanh toán & Chương trình Khách hàng thân thiết (Loyalty Program)
- Tích hợp thêm các ví điện tử phổ biến tại Việt Nam như **MoMo, ZaloPay, Viettel Money, Apple Pay và Google Pay**.
- Xây dựng phân hệ **Thẻ thành viên & Tích điểm đổi quà (Loyalty Points & Tier System)**: Phân hạng thành viên (Bạc, Vàng, Kim Cương), cho phép đổi điểm lấy vé miễn phí hoặc combo bắp nước.

---

### TỔNG KẾT CHƯƠNG 4
Dự án Cinezone đã chứng minh tính đúng đắn và hiệu quả của giải pháp công nghệ đã lựa chọn. Dù còn một số hạn chế cần tiếp tục hoàn thiện, hệ thống đã tạo nền tảng vững chắc sẵn sàng đáp ứng yêu cầu vận hành thực tế của một chuỗi rạp chiếu phim hiện đại trong kỷ nguyên số.
