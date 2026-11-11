# EasyToDoList CI/CD Design

## Intent

Giữ `main` luôn build/test được và tạo gói Windows có thể tải về từ mỗi tag phiên bản, nhưng không dựng quy trình installer trước khi ứng dụng đến M12.

## Goals

- CI chạy trên pull request vào `main` và push lên `main`.
- CI dùng Windows runner, restore, build Release với warnings-as-errors và chạy toàn bộ test.
- CD chạy khi push tag khớp `v*`.
- CD chỉ tiếp tục sau khi build/test thành công, publish `win-x64` self-contained và upload artifact.
- Workflow có concurrency để hủy run cũ trên cùng branch hoặc tag.
- GitHub Actions chỉ dùng quyền đọc repository; CD dùng quyền ghi `contents` khi M12 bổ sung GitHub Release.

## Non-goals

- Chưa build Inno Setup installer.
- Chưa tạo GitHub Release tự động.
- Chưa ký code, phát hành Microsoft Store hoặc deploy server.
- Không chạy workflow thành công giả khi solution chưa tồn tại.

## Approaches considered

1. **CI và tag-based CD skeleton (selected):** kiểm tra PR/push và tạo artifact self-contained từ tag. Cho phép kiểm chứng sớm đường phát hành mà không kéo M12 vào hiện tại.
2. **Chỉ CI đến M12:** ít cấu hình sớm hơn nhưng không phát hiện lỗi publish trước giai đoạn release.
3. **Installer/release đầy đủ ngay:** cho đầu ra gần sản phẩm nhất nhưng sai thứ tự vì repository chưa có solution và installer.

## Workflow design

### CI

- File: `.github/workflows/ci.yml`.
- Trigger: `pull_request` vào `main`, `push` vào `main`, và `workflow_dispatch`.
- Runner: `windows-latest`.
- Steps sau khi M01 tạo solution: checkout, setup .NET 10, restore locked dependencies khi lock file có mặt, build Release với `--no-restore -warnaserror`, test Release với `--no-build`.
- Trước M01, workflow được giữ trong tài liệu thay vì commit một workflow luôn thất bại.

### CD

- File: `.github/workflows/release.yml`.
- Trigger: tag `v*` và `workflow_dispatch`.
- Runner: `windows-latest`.
- Steps sau M01: checkout, setup .NET 10, restore, build/test, publish WPF App cho `win-x64` self-contained, upload artifact có retention hữu hạn.
- Tại M12: thêm Inno Setup, checksum, GitHub Release và installer vào cùng pipeline.

## Failure and security policy

- Không dùng secret trong CI cơ bản.
- Pin action theo major version ổn định; cập nhật định kỳ qua Dependabot ở giai đoạn bảo trì.
- Một lỗi restore, build, test hoặc publish làm job thất bại; không dùng `continue-on-error`.
- Artifact chỉ là build thử cho tới khi M12 hoàn tất kiểm thử cài đặt và ký/phát hành chính thức.

## Rollout

1. M01 tạo solution và project paths.
2. Trong cùng M01, thêm CI rồi xác nhận một run xanh trên `main`.
3. Thêm CD skeleton sau khi lệnh `dotnet publish` chạy cục bộ thành công.
4. M12 mở rộng CD thành installer và GitHub Release.

