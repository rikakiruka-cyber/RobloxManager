# Roblox RAM Manager

Account manager + RAM/CPU optimizer for Roblox · Quản lý tài khoản + tối ưu RAM/CPU cho Roblox.

**By RikaSoora** · [Discord](https://discord.com/users/1374334753242873908) · See [LICENSE](LICENSE) — please don't re-upload this as your own work. / Vui lòng không đăng lại như sản phẩm của bạn.

## Build

Install **.NET SDK 10**, then run `build_exe.bat` → `Release_EXE\RobloxManager_<version>_x64.exe` (single file).

## Release an update · Phát hành bản cập nhật

1. One-time / Làm 1 lần: install [Git](https://git-scm.com/download/win), [GitHub CLI](https://cli.github.com) (`winget install GitHub.cli`), then `gh auth login`.
2. Double-click **`push_update.bat`** / Bấm đúp **`push_update.bat`**:
   - choose the new version (auto bump) · chọn version mới (tự tăng)
   - type what's new (Vietnamese + English) · nhập "có gì mới" (tiếng Việt + Anh)
   - it builds the exe, pushes the code and publishes a GitHub Release · tự build, đẩy code và tạo GitHub Release
3. Every user's app checks the latest Release on start, shows "What's new" and updates itself.
   App của mọi người tự kiểm tra Release mới khi mở, hiện "có gì mới" và tự cập nhật.

The updater reads `UpdateRepo` in `App/Updater.cs`. The repo must be **Public** (or only the Releases repo must be public — see below).
Updater đọc `UpdateRepo` trong `App/Updater.cs`; repo đó phải **Public**.

### Keep the source private? · Muốn giấu source?

Create a second **public** repo (e.g. `RobloxManager-releases`, with just a README), set `UpdateRepo` to it in `App/Updater.cs`, and keep this repo **Private**. `push_update.bat` pushes code to this repo (`origin`) and publishes the exe to the releases repo.
Tạo repo **public** thứ hai (vd `RobloxManager-releases`, chỉ cần 1 README), đổi `UpdateRepo` sang repo đó, còn repo này để **Private**.
