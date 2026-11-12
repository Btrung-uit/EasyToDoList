# M01 Solution Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tạo solution WPF .NET 10 build được, database SQLite tự migrate/seed trong thư mục dữ liệu người dùng và workflow CI/CD khớp project thật.

**Architecture:** Giữ dependency `App -> Core + Infrastructure`, `Infrastructure -> Core`, `Core -> none`. EF Core migration là nguồn tạo schema; WPF startup khởi tạo database trước khi hiện `MainWindow`.

**Tech Stack:** .NET 10 LTS, WPF, EF Core SQLite 10, xUnit, GitHub Actions

**Spec:** `docs/superpowers/specs/2026-11-11-easy-to-do-list-design.md`

**Architecture:** `docs/architecture/system-design.md`

**CI/CD design:** `docs/superpowers/specs/2026-11-11-ci-cd-design.md`

## Global Constraints

- Target `net10.0`; WPF và App tests target `net10.0-windows`.
- Bật nullable, implicit usings và warnings-as-errors cho toàn solution.
- Database production nằm dưới `%LOCALAPPDATA%/EasyToDoList`, không cạnh executable.
- Production chỉ dùng EF Core migration, không dùng `EnsureCreated`.
- CI không dùng secret và không bỏ qua lỗi restore/build/test/publish.

## Review Focus

1. Khởi tạo lặp lại không tạo profile thứ hai — Task 2 test gọi initializer hai lần.
2. Đường dẫn database không rơi về working directory — Task 2 test xác nhận nằm dưới LocalApplicationData.
3. Migration chạy trên database file mới — Task 2 integration test xác nhận file và schema/profile.
4. Core vô tình reference layer khác — Task 1 kiểm tra project references trong solution.
5. Workflow trỏ sai solution/project — Task 3 chạy đúng các lệnh workflow ở local.

---

### Task 1: Scaffold solution boundaries

**Files:**
- Create: `Directory.Build.props`
- Create: `global.json`
- Create: `EasyToDoList.sln`
- Create: `src/EasyToDoList.App/*`
- Create: `src/EasyToDoList.Core/*`
- Create: `src/EasyToDoList.Infrastructure/*`
- Create: `tests/EasyToDoList.App.Tests/*`
- Create: `tests/EasyToDoList.Core.Tests/*`
- Create: `tests/EasyToDoList.Infrastructure.Tests/*`

**Interfaces:**
- Produces: solution/project references and a WPF window that can start.

- [x] Scaffold three product projects and three xUnit projects.
- [x] Add references: App to Core/Infrastructure; Infrastructure to Core; matching test projects to their product project.
- [x] Pin SDK `10.0.401`; centralize nullable, implicit usings and warnings-as-errors.
- [x] Remove generated placeholder classes/tests.
- [x] Run `dotnet restore EasyToDoList.sln` and `dotnet build EasyToDoList.sln -warnaserror`.

### Task 2: SQLite migration bootstrap

**Files:**
- Create: `src/EasyToDoList.Infrastructure/Persistence/ApplicationDataPaths.cs`
- Create: `src/EasyToDoList.Infrastructure/Persistence/AppDbContext.cs`
- Create: `src/EasyToDoList.Infrastructure/Persistence/PlayerProfileRow.cs`
- Create: `src/EasyToDoList.Infrastructure/Persistence/DatabaseInitializer.cs`
- Create: `src/EasyToDoList.Infrastructure/Persistence/Migrations/*`
- Modify: `src/EasyToDoList.App/App.xaml`
- Modify: `src/EasyToDoList.App/App.xaml.cs`
- Test: `tests/EasyToDoList.Infrastructure.Tests/Persistence/ApplicationDataPathsTests.cs`
- Test: `tests/EasyToDoList.Infrastructure.Tests/Persistence/DatabaseInitializerTests.cs`

**Interfaces:**
- Produces: `ApplicationDataPaths.DatabasePath`, `AppDbContext`, `DatabaseInitializer.InitializeAsync(CancellationToken)`.

- [x] Write `DatabasePath_is_under_local_application_data` and watch it fail because `ApplicationDataPaths` is missing.
- [x] Implement `ApplicationDataPaths.DatabasePath` with `%LOCALAPPDATA%/EasyToDoList/easytodolist.db`; rerun test green.
- [x] Write `Initialize_creates_database_and_single_profile` and `Initialize_twice_keeps_single_profile`; watch them fail because persistence types are missing.
- [x] Implement EF Core context/entity/initializer and initial migration with profile ID `1`, EXP `0`, coin `0`.
- [x] Wire WPF startup to create the directory, migrate database, then show `MainWindow`.
- [x] Run the infrastructure tests and full suite.

### Task 3: CI/CD workflows and milestone verification

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/release.yml`
- Modify: `TASKS.md`

**Interfaces:**
- Consumes: `EasyToDoList.sln` and `src/EasyToDoList.App/EasyToDoList.App.csproj`.
- Produces: Windows CI on PR/push main and self-contained `win-x64` artifact on `v*` tags/manual runs.

- [x] Add CI checkout/setup-dotnet/restore/build/test steps with read-only permissions and concurrency.
- [x] Add release build/test/publish/upload-artifact steps; do not create installer or GitHub Release yet.
- [x] Run the workflow-equivalent restore/build/test/publish commands locally.
- [x] Mark every M01 checkbox and status complete only after verification succeeds.
- [x] Run `git diff --check`, `dotnet build EasyToDoList.sln -c Release -warnaserror`, and `dotnet test EasyToDoList.sln -c Release --no-build`.
- [x] Commit as `chore: scaffold WPF solution and SQLite persistence`.
