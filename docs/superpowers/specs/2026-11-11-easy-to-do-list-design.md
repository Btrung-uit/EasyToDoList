# EasyToDoList — Đặc tả thiết kế

**Ngày chốt:** 2026-11-11  
**Trạng thái:** Đã duyệt; xem system design tại [`docs/architecture/system-design.md`](../../architecture/system-design.md)

## 1. Ý tưởng và mục tiêu

EasyToDoList là ứng dụng năng suất cá nhân chạy trên Windows, kết hợp To-do và Pomodoro trong phong cách pixel-art medieval dễ thương. Ứng dụng tạo cảm giác hoàn thành giống trò chơi: công việc vỡ thành pixel, combo xuất hiện khi hoàn thành liên tiếp, và người dùng tiến bộ qua EXP, level, rank và coin.

Mục tiêu của phiên bản đầu:

- Giúp người dùng luôn thấy việc cần làm hôm nay mà không chiếm nhiều diện tích màn hình.
- Tạo động lực hoàn thành sớm bằng thưởng EXP/coin và tạo áp lực vừa phải bằng hình phạt quá hạn hoặc hủy việc.
- Hỗ trợ tập trung bằng Pomodoro có giới hạn thời gian hợp lý.
- Hoạt động hoàn toàn offline, nhanh và không cần tài khoản.
- Xây một nền tảng đủ sạch để sau này bổ sung cửa hàng cosmetic và dịch vụ đồng bộ, nhưng không xây trước các phần đó.

## 2. Ngoài phạm vi MVP

Phiên bản đầu chưa có:

- Tài khoản, cloud sync, PostgreSQL, web hoặc mobile.
- Cửa hàng sử dụng được, inventory hoặc thanh toán thật. MVP vẫn tích coin và lưu lịch sử coin để dùng cho cửa hàng sau này.
- Achievement, nhiệm vụ ngày, bảng xếp hạng hoặc multiplayer.
- Subtask, tag, project, To-do lặp lại hoặc cộng tác.
- Vật phẩm thay đổi EXP, xóa phạt, tự hoàn thành công việc hoặc tạo lợi thế năng suất.
- Thống kê nâng cao và import/export dữ liệu.

## 3. Các hướng kỹ thuật đã cân nhắc

### Hướng được chọn: WPF + SQLite

- Ứng dụng WPF/.NET chạy cục bộ trên Windows.
- MVP hỗ trợ chính thức Windows 11 bản x64. Windows 10 22H2 chỉ là tương thích best-effort, không phải release gate vì hệ điều hành này đã hết hỗ trợ chính thức.
- Phân phối bằng installer self-contained; không yêu cầu người dùng cài .NET trước và chưa phát hành qua Microsoft Store.
- SQLite lưu dữ liệu offline; dùng migration để quản lý schema.
- ID là GUID. Timestamp hệ thống lưu UTC; ngày bắt đầu, deadline và phép tính qua ngày dùng múi giờ địa phương của máy.
- Khi triển khai sẽ chọn phiên bản .NET còn được hỗ trợ và SQLite provider tương thích tại thời điểm đó.

Lý do chọn: ít thành phần nhất để hoàn thiện trải nghiệm cốt lõi, không cần server, chạy được khi mất mạng và phù hợp nền tảng hiện tại của chủ dự án.

### Hướng không chọn: WPF + API + PostgreSQL ngay

Hướng này hỗ trợ tài khoản và đa thiết bị sớm nhưng buộc phải xây thêm xác thực, hosting, bảo mật, đồng bộ offline và giải quyết xung đột dữ liệu trước khi To-do/Pomodoro được kiểm chứng.

### Hướng bị loại: WPF kết nối trực tiếp PostgreSQL

Client desktop không được kết nối trực tiếp cơ sở dữ liệu trên Internet vì credentials có thể bị lấy và người dùng có thể sửa dữ liệu EXP/coin. Khi cần đa nền tảng, kiến trúc tương lai sẽ là client → ASP.NET Core API → PostgreSQL.

## 4. Hai dạng giao diện

Chỉ một dạng được hiển thị tại một thời điểm.

### 4.1. Compact Overlay

- Luôn nổi trên các cửa sổ khác.
- Có thể kéo tới vị trí mong muốn.
- Chỉ hiển thị một trong hai chế độ con:
  - **To-do:** danh sách công việc hiện tại, tick hoàn thành, combo và nút mở Dashboard.
  - **Pomodoro:** thời gian còn lại, trạng thái Tập trung/Nghỉ, Pause/Resume, Skip, Stop và nút mở Dashboard.
- Không tạo hoặc chỉnh sửa chi tiết To-do trong Overlay.
- Chuyển từ Overlay sang Dashboard sẽ ẩn Overlay; Pomodoro đang chạy không bị gián đoạn.

### 4.2. Dashboard

Dashboard là cửa sổ quản lý đầy đủ và không luôn nổi. Các khu vực chính:

- Hôm nay: công việc đang hoạt động và quá hạn.
- Lịch tuần/lịch sử theo ngày.
- Tạo, sửa, hủy và hoàn thành To-do.
- Pomodoro và cấu hình thời lượng.
- Hồ sơ: level, rank, EXP và coin.
- Lịch sử giao dịch EXP/coin.
- Settings.

Nút **Thu gọn thành Overlay** sẽ ẩn Dashboard và mở Overlay. Nút `X` thu ứng dụng xuống system tray, không tự đổi dạng. Menu tray có Open Overlay, Open Dashboard và Exit. Có tùy chọn khởi động cùng Windows.

## 5. To-do và vòng đời công việc

Mỗi To-do tối thiểu có:

- Tiêu đề.
- Ngày bắt đầu.
- Deadline theo ngày.
- Thời điểm tạo và cập nhật.
- Thời điểm hoàn thành hoặc hủy, nếu có.
- Trạng thái.
- Mốc thưởng đã khóa và cờ xác nhận đã áp dụng phạt quá hạn.

Ngày bắt đầu và deadline được tính bao gồm cả hai đầu. Deadline không được trước ngày bắt đầu.

Các trạng thái:

- **Scheduled:** chưa tới ngày bắt đầu.
- **Active:** từ ngày bắt đầu tới hết deadline.
- **Completed:** đã hoàn thành.
- **Overdue:** qua 00:00 ngày kế tiếp deadline mà chưa hoàn thành/hủy.
- **Cancelled:** người dùng chủ động hủy.

Danh sách hôm nay hiển thị Active và Overdue. Scheduled nằm trong lịch. Completed và Cancelled nằm trong lịch sử.

### 5.1. Sửa và hủy

- To-do chưa lưu chỉ là bản nháp và có thể bỏ miễn phí.
- Sau khi lưu, hủy To-do trước hoặc trong deadline bị trừ `10% EXP cơ bản`, làm tròn và giới hạn từ 2 đến 10 EXP.
- Hủy To-do đã Overdue không chịu thêm phí hủy, nhưng giữ nguyên khoản phạt quá hạn và không được hoàn lại EXP.
- To-do bị hủy vẫn nằm trong lịch sử; không xóa cứng để né phạt.
- Deadline có thể sửa tự do trước ngày bắt đầu.
- Sau khi bắt đầu, dời deadline muộn hơn sẽ làm To-do mất quyền nhận thưởng hoàn thành sớm. Rút ngắn deadline vẫn dùng công thức thưởng bình thường.
- `D`, `B`, ngày bắt đầu tính thưởng và deadline tính thưởng được khóa vào 00:00 ngày bắt đầu theo giờ máy. Nếu lúc đó ứng dụng không chạy, các giá trị được khóa ở lần mở tiếp theo từ lịch đã lưu trước khi bước sang ngày bắt đầu.
- Trước ngày bắt đầu, chỉnh lịch sẽ cập nhật các mốc thưởng dự kiến. Sau khi khóa, thay đổi deadline hiển thị không làm tăng `B`; dời deadline muộn hơn mất thưởng sớm, còn rút ngắn deadline tính phần thưởng sớm trên deadline mới nhưng vẫn dùng `B` đã khóa.
- Các mốc tính thưởng được lưu cùng To-do để sửa dữ liệu không tạo thêm EXP.

## 6. EXP từ To-do

Gọi:

- `D`: số ngày theo lịch từ ngày bắt đầu đến deadline, tính cả hai đầu.
- `B`: EXP cơ bản.
- `R`: số ngày kế hoạch còn lại sau ngày hoàn thành, giới hạn từ `0` đến `D - 1`.

```text
B = 20 + 5 × min(D - 1, 13)
```

Ví dụ:

| Thời lượng | EXP cơ bản |
|---:|---:|
| 1 ngày | 20 |
| 2 ngày | 25 |
| 7 ngày | 50 |
| 14 ngày trở lên | 85 |

To-do có thể dài hơn 14 ngày nhưng EXP cơ bản không vượt 85 để hạn chế farm bằng deadline dài.

### 6.1. Hoàn thành đúng hạn hoặc sớm

```text
Nếu D = 1: thưởng sớm E = 0
Nếu D > 1: E = round(B × 0.5 × R / (D - 1))
EXP nhận = B + E
```

- Hoàn thành vào ngày bắt đầu có thể nhận tối đa `50% B` thưởng sớm.
- Hoàn thành vào deadline có `E = 0`.
- Không nhân EXP theo combo.

### 6.2. Quá hạn

- Tại lần đầu ứng dụng xác định đã qua 00:00 sau deadline, trừ một lần `round(30% B)`.
- Không tiếp tục trừ theo từng ngày quá hạn.
- Khi hoàn thành một To-do Overdue, hoàn lại `round(25% B)` nhưng không nhận coin.
- Nếu ứng dụng không chạy lúc qua ngày, khoản phạt được áp dụng đúng một lần ở lần mở tiếp theo.
- Tổng EXP tài khoản không bao giờ thấp hơn 0.

Thiết kế này khiến hoàn thành trễ vẫn tốt hơn bỏ cuộc nhưng kết quả ròng của công việc trễ vẫn âm nhẹ.

## 7. Level và rank

EXP cần để đi từ level hiện tại sang level kế tiếp:

```text
100 + 25 × (level - 1)
```

Rank:

| Rank | Level | Danh hiệu |
|---|---:|---|
| F | 1–4 | Tân binh |
| E | 5–9 | Học việc |
| D | 10–19 | Nhà thám hiểm |
| C | 20–34 | Chiến binh |
| B | 35–54 | Hiệp sĩ |
| A | 55–79 | Anh hùng |
| S | 80+ | Huyền thoại |

Hình phạt có thể làm level và rank giảm. Mức thấp nhất là level 1 với 0 EXP.

## 8. Coin và cửa hàng tương lai

Coin là tiền cosmetic, không thay thế EXP và không bị trừ do quá hạn/hủy việc.

### Coin từ To-do

```text
Coin = floor(B / 5) + floor(E / 10)
```

- Chỉ To-do hoàn thành trong deadline nhận coin.
- To-do Overdue hoàn thành không nhận coin.
- Combo không nhân coin.

### Coin từ Pomodoro

```text
Coin = floor(số phút tập trung / 5)
```

Coin không bao giờ âm. Khi xây cửa hàng, coin chỉ mua cosmetic như theme medieval, khung hồ sơ, hiệu ứng vỡ/combo, sound pack, linh vật và animation Pomodoro. Không bán lợi thế năng suất hoặc phần thưởng thật.

MVP chưa xây shop/inventory; chỉ lưu số dư và ledger coin để không mất tiến trình sau này.

## 9. Animation hoàn thành và combo

### Animation hoàn thành

Tổng thời lượng mục tiêu `700–900 ms`:

1. Vết nứt xuất hiện.
2. Ánh sáng lóe qua vết nứt.
3. To-do vỡ thành pixel và biến mất kèm âm thanh.
4. Khoảng trống thu lại và các mục dưới dồn lên.
5. Hiện phần thưởng EXP/coin.

Animation không khóa toàn bộ giao diện; người dùng vẫn có thể tick mục khác.

### Combo

- Lần hoàn thành đầu mở vòng combo 2 giây.
- Một To-do khác hoàn thành trong 2 giây sẽ tăng `x2`, `x3`, ... và reset vòng đếm về 2 giây.
- Mỗi bậc phát âm thanh liên hoàn cao hơn theo phong cách game.
- Hết thời gian, combo biến mất bằng hiệu ứng pixel.
- Combo chỉ để tạo cảm giác thỏa mãn, không thay đổi EXP hoặc coin.

### Undo

- Sau khi hoàn thành có thể Undo trong 5 giây.
- Undo khôi phục To-do và đảo chính xác các giao dịch EXP/coin đã tạo.
- Nếu vòng combo vẫn đang hoạt động, bộ đếm combo được điều chỉnh; hiệu ứng đã phát không cần phát ngược.

## 10. Pomodoro

### 10.1. Preset

| Chế độ | Tập trung | Nghỉ ngắn | Nghỉ dài |
|---|---:|---:|---:|
| Khởi động | 15 phút | 3 phút | 15 phút |
| Tiêu chuẩn | 25 phút | 5 phút | 20 phút |
| Tập trung sâu | 50 phút | 10 phút | 25 phút |

Sau 4 phiên tập trung hoàn thành sẽ tới nghỉ dài.

### 10.2. Tùy chỉnh hợp lệ

- Tập trung: 15–60 phút.
- Nghỉ ngắn: từ 20% đến 30% thời gian tập trung.
- Nghỉ dài: 15–30 phút và phải dài hơn nghỉ ngắn.
- Nghỉ dài sau 3 hoặc 4 phiên tập trung.
- Giao diện phải giải thích điều kiện bị vi phạm thay vì chỉ vô hiệu hóa nút lưu.

Ví dụ `40/1`, `60/5` và `10/60` không hợp lệ; `40/8`, `45/10` và `60/15` hợp lệ.

### 10.3. Điều khiển và chuyển phiên

- Start, Pause/Resume, Skip và Stop.
- Skip một phiên tập trung không nhận thưởng.
- Stop cần xác nhận và không nhận thưởng cho phần phiên chưa hoàn thành.
- Hoàn thành một phiên phát chuông, trao thưởng và dừng ở màn hình chờ; người dùng chủ động bắt đầu phiên nghỉ/tập trung kế tiếp.
- Nghỉ không trao EXP hoặc coin.
- Khi gần hết phiên có tiếng tick; có thể tắt trong Settings.

### 10.4. EXP Pomodoro

```text
EXP = 2 × floor(số phút tập trung / 5)
```

Ví dụ: 15 phút = 6 EXP; 25 phút = 10 EXP; 40 phút = 16 EXP; 50 phút = 20 EXP; 60 phút = 24 EXP. Hai phiên 25 phút trao tổng phần thưởng bằng một phiên 50 phút để không ép người dùng chọn phiên quá dài.

Chỉ trao EXP/coin một lần khi hoàn thành toàn bộ phiên tập trung.

### 10.5. Gián đoạn và khôi phục

- Chuyển giữa Dashboard, Overlay và system tray không làm dừng Pomodoro vì ứng dụng vẫn chạy.
- Khi máy sleep hoặc người dùng Exit ứng dụng, phiên được tự động Pause và lưu trạng thái.
- Lưu thời điểm bắt đầu, thời điểm kết thúc dự kiến và thời gian còn lại để khôi phục nhất quán.
- Khi mở lại hoặc máy thức dậy, phiên vẫn Pause và không tính thời gian ứng dụng/máy bị gián đoạn.
- Không tự trao thưởng khi ứng dụng không chạy.

## 11. Âm thanh, hình ảnh và khả năng tiếp cận

- Phong cách nhất quán: cute pixel-art medieval.
- Trạng thái tập trung và nghỉ có animation riêng, ví dụ nhân vật luyện phép/đọc sách và nghỉ bên đống lửa.
- Settings có âm lượng tổng, âm thanh hoàn thành, combo, tick và chuông.
- Có thể tắt animation hoặc dùng Reduced Motion.
- Reduced Motion vẫn phải thể hiện rõ trạng thái hoàn thành bằng màu/chữ, không phụ thuộc duy nhất vào chuyển động hoặc âm thanh.
- Pixel-art phải hiển thị sắc nét khi Windows scaling thay đổi.
- Trong quá trình phát triển có thể dùng placeholder nhất quán, nhưng bản MVP được xem là hoàn thành chỉ khi có một bộ asset và âm thanh medieval pixel-art tối thiểu, đồng bộ và có quyền sử dụng rõ ràng.

## 12. Thông báo và Settings

Windows notification được dùng cho:

- Pomodoro kết thúc.
- To-do tới deadline.
- To-do vừa chuyển sang Overdue.

Mỗi loại có thể bật/tắt riêng. Settings còn chứa preset/custom Pomodoro, âm lượng, animation, Reduced Motion, khởi động cùng Windows và hành vi thông báo.

Thông báo chỉ hoạt động khi ứng dụng đang mở hoặc nằm trong system tray. Chọn `Exit` là dừng hoàn toàn tiến trình; ứng dụng không chạy background task và không gửi thông báo cho tới lần mở tiếp theo.

## 13. Dữ liệu và tính toàn vẹn

Tối thiểu cần các nhóm dữ liệu:

- To-do và lịch sử trạng thái.
- Pomodoro session.
- Hồ sơ level/rank và số dư coin.
- Ledger EXP.
- Ledger coin.
- Settings.

Mỗi thay đổi EXP/coin là một giao dịch có ID, nguồn, lý do, số lượng và thời điểm. Các sự kiện thưởng/phạt phải có khóa duy nhất theo nguồn để không áp dụng hai lần khi ứng dụng crash hoặc mở lại. Undo tạo giao dịch đảo, không xóa lịch sử cũ.

Vì đây là ứng dụng offline cá nhân, MVP không cố chống người dùng tự sửa file SQLite. Mục tiêu là ngăn lỗi ứng dụng cộng/trừ trùng, không xây hệ thống chống gian lận.

## 14. Các luồng cốt lõi cần hoạt động

1. Tạo To-do hợp lệ → xuất hiện đúng ngày → hoàn thành → animation → EXP/coin → lịch sử.
2. Hoàn thành nhiều To-do liên tiếp → combo reset 2 giây → không nhân phần thưởng.
3. Undo hoàn thành → khôi phục To-do và đảo thưởng đúng một lần.
4. To-do qua deadline khi ứng dụng đang chạy hoặc đã đóng → phạt đúng một lần.
5. Hủy To-do → áp dụng đúng mức phạt và giữ lịch sử.
6. Chạy Pomodoro hợp lệ → Pause/Resume → hoàn thành → EXP/coin đúng một lần.
7. Sleep hoặc Exit giữa Pomodoro → mở lại ở trạng thái Pause với thời gian còn lại phù hợp.
8. Chuyển Dashboard ↔ Overlay ↔ tray → chỉ một dạng chính hiển thị, dữ liệu và timer không mất.

## 15. Rủi ro đã biết

- Asset pixel-art, particle animation và âm thanh có thể chiếm nhiều thời gian hơn logic ứng dụng; cần ưu tiên một bộ asset nhỏ nhưng nhất quán cho MVP.
- Hệ thống phạt có thể gây áp lực quá mức; các tỷ lệ phải được lưu dưới dạng quy tắc nội bộ dễ hiệu chỉnh sau thử nghiệm, nhưng MVP không cần màn hình cấu hình economy.
- Ngày giờ máy có thể bị thay đổi; MVP chấp nhận giới hạn này vì là ứng dụng offline cá nhân.
- Cloud sync và cửa hàng thật sẽ cần thiết kế riêng sau khi trải nghiệm cốt lõi được kiểm chứng.

