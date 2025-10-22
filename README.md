<<<<<<<<<<<<<<<<<<<<<<<WELL DONE>>>>>>>>>>>>>>>>>>>>>>>>

BUILD:
-KẾT NỐI DB THÀNH CÔNG
-MAPPING OK CÁC CLASS(model)-TABLE(bảng db)
-XONG PHẦN ĐĂNG KÝ/ĐĂNG NHẬP : Mức xử lý dữ liệu và lưu trữ
-XONG ĐIỀU HƯỚNG ĐẾN VIEW CỦA TỪNG ĐỐI TƯỢNG

Các trang gồm:
Chung:
-Home: Hiểm thị sản phẩm hót, được mua nhiều nhất, các chương trình khuyến mãi, các sự kiện, Các mẫu mới,....
-Product: Hiểm thị các sản phẩm mới, các sản phẩm theo từng loại, hỗ trợ tìm kiếm,...
-Cart: Hiểm thị giỏ hàng của khách hàng, các thông tin chi tiết về đơn hàng, tình trạng giao hàng,... 
-Error: Báo lỗi khi trang không truy cập được

Riêng:
-Customer: Hiển thị thông tin khách hàng, cài đặt về tài khoản và các thông tin cá nhân
-Staff: Hiển thị thông tin về nhân viên, các chức năng của nhân viên trong use case
-Store: Hiển thị thông tin về cửa hàng, các chức năng trong use case
-Director: Hiển thị tên giám đốc và chức vụ, các chức năng trong use case
-Admin: Hiển thị tên, các chức năng trong use case, không liên quan đến mảng kinh doanh

VẤN ĐỀ: 
-VIỆC CHIA RA TỪNG CHI NHÁNH CỬA HÀNG SẼ VÔ NGHĨA NẾU KHÁCH HÀNG KHÔNG TIẾP CẬN 
		TỪNG CỬA HÀNG, VÌ VẬY SẼ KHÔNG THỐNG KÊ ĐƯỢC DỮ LIỆU THEO CỬA HÀNG
==> ĐÃ XONG, CHIA NHÁNH XONG, PHÂN LUỒNG XONG
--------------------
Các chức năng chính
Đối với Khách hàng: DONE
•	Đăng ký / Đăng nhập: OK
•	Xem danh sách sản phẩm: OK
•	Tìm kiếm sản phẩm theo tên, loại, giá: OK
•	Thêm sản phẩm vào giỏ hàng: OK
•	Đặt hàng & thanh toán: OK
•	Theo dõi tình trạng đơn hàng: OK
•	Đánh giá, phản hồi sản phẩm: OK
Đối với Nhân viên:
•	Quản lý sản phẩm (thêm, sửa, xóa, cập nhật số lượng), danh mục: OK - thiếu khuyến mãi
•	Quản lý đơn hàng: OK
•	Quản lý thông tin khách hàng (Chỉ xem xếp hạng thành viên): OK
•	Hỗ trợ xử lý yêu cầu, phản hồi: OK
Đối với Chủ cửa hàng:
•	Theo dõi báo cáo doanh thu của chi nhánh: OK
•	Quản lý nhân viên: OK
•	Kiểm tra đơn hàng của cửa hàng: OK
•	Thống kê và phân tích dữ liệu bán hàng của chi nhánh: OK
Đối với Giám đốc hệ thống:
•	Quản lý toàn bộ chuỗi cửa hàng: OK
•	Thống kê và phân tích dữ liệu bán hàng tổng thể: OK
Đối với quản trị viên:
•	Xem thông tin hệ thống: OK
•	Quản lý hệ thống: OK

----------------
DEPLOY: CHƯA CÓ GÌ


+++++++++THƯ MỤC wwwroot/logs/... lưu log của run 5 procedure+++++++++++++++++
TRONG DB: Bảng Orders, trường IdEmployee không được liên kết FK với bảng Employee
Lý do là để lưu được giá trị 0: Nghĩa là đơn hàng chưa được nhân viên xử lý.
Luồng dữ liệu đơn hàng: 
-Khách hàng-->Đổ đơn về Store
-Nhân viên--->Xử lý đơn hàng trên Store
Tác dụng: Động tác nhỏ này giúp khách hàng mua hàng không cần nhân viên, nhưng vẫn biết được là nhân viên nào xử lý đơn hàng nào
Từ đó mà ghi nhận được công sức làm việc của nhân viên.