# EasyToDoList Agent Rules

Phạm vi: toàn repo. Mục tiêu: ít token; không giảm correctness, an toàn dữ liệu, test, accessibility.

## Luồng mặc định

1. Đọc `AGENTS.md`, đúng milestone trong `TASKS.md`, file trực tiếp liên quan.
2. `rg -n`/`rg --files` tìm symbol, caller, test. Không dump cây/file dài.
3. Chốt scope: code, caller, test, migration/config. Thay đổi nhỏ không cần plan riêng.
4. Tái dùng pattern; diff nhỏ nhất đạt acceptance. Không abstraction/dependency/config “để sau”.
5. Logic mới: test đỏ tập trung → patch tối thiểu → xanh.
6. Cuối milestone: full test, build, runtime phù hợp, staged diff, một commit.

## Đọc và tìm kiếm

- Đọc đoạn cần thiết; full file chỉ khi cần toàn contract. Không đọc lại file không đổi.
- Không mở toàn docs; theo link khi quyết định phụ thuộc nó. Không browse nếu repo đã trả lời.
- Filter output, đặt `max_output_tokens`; không in binary/generated lớn/base64.

## Build và test

- Restore một lần, sau đó `--no-restore`. Dùng `-m:1 -p:UseSharedCompilation=false`; không chạy song song lệnh ghi `obj/bin`.
- Khi code: test project/filter bị tác động. Không full solution sau mỗi patch.
- Cuối task, một lần: `dotnet test EasyToDoList.sln --no-restore -m:1 -p:UseSharedCompilation=false --verbosity minimal` rồi `dotnet build EasyToDoList.sln --no-restore -warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal`.
- Chỉ dùng `-v:diag` sau khi verbosity tối thiểu và process/build state không tìm ra nguyên nhân.

## Debug

- Giữ root cause + evidence, không giữ log thừa. Hai lần cùng lỗi: dừng thử mò; kiểm tra process, environment, diff, working example.
- Sửa root cause chung. WPF: launch một lần sau batch UI cuối; kiểm tra response, binding, navigation/accessibility; đóng sạch.

## UI

- Batch XAML/ViewModel theo feature slice. Dùng token/component sẵn; không thêm style/asset ngoài acceptance.
- UI detector một lần sau edit cuối; sửa runtime sau đó chỉ check trực tiếp phần bị ảnh hưởng.

## Agent, skill và review

- Chỉ dùng skill bắt buộc/trực tiếp hữu ích; đọc `SKILL.md` một lần, reference được route tới.
- Không agent cho việc nhỏ/tuần tự. Reviewer nhận snapshot freeze; không sửa khi review chạy; một reviewer đủ.

## Giao tiếp

- Commentary lúc bắt đầu, đổi phase, lỗi/blocker, hoặc >60 giây; không kể từng command.
- Không lặp plan/log/diff/kết quả. Final: outcome, verification, file chính, rủi ro.
- Chỉ hỏi khi lựa chọn đổi scope/behavior đáng kể; còn lại dùng default đã chốt.

## Git

- Giữ thay đổi ngoài task. Stage đúng file; `git diff --cached --check` trước commit.
- Một milestone = một branch/worktree + một commit. Không merge trước CI xanh và lệnh tiếp tục.

## Không được tiết kiệm

- Validation, transaction/idempotency, migration compatibility, chống mất dữ liệu.
- Test logic không tầm thường; keyboard/accessibility; runtime proof WPF.
- Không báo pass/completed thiếu evidence mới từ snapshot cuối.
