# Roblox RAM Manager

Account manager + RAM/CPU optimizer for Roblox · Quản lý tài khoản + tối ưu RAM/CPU cho Roblox.

**By RikaSoora** · [Discord](https://discord.com/users/1374334753242873908) · See [LICENSE](LICENSE) — please don't re-upload this as your own work. / Vui lòng không đăng lại như sản phẩm của bạn.

---

## Download · Tải về

Get the latest `RobloxManager_<version>_x64.exe` from the **[Releases page](../../releases/latest)** (Windows 10/11, 64-bit). It is a single file, no installer needed.
Tải file `RobloxManager_<version>_x64.exe` mới nhất ở trang **[Releases](../../releases/latest)** (Windows 10/11, 64-bit). Chỉ 1 file, không cần cài đặt.

> Only download from this repo's Releases page. Copies from other sites may have been modified.
> Chỉ tải từ trang Releases của repo này. Bản từ nơi khác có thể đã bị chỉnh sửa.

---

## Is it a virus? Is it safe for my PC? · Có virus không? Có an toàn cho máy không?

**English** — It is not a virus, and you can check that yourself: the full source code is in this repo. But it is a small hobby tool that is **not code-signed**, so Windows or your antivirus may warn you the first time. That does not mean it is malicious.

**Tiếng Việt** — Đây không phải virus, và bạn có thể tự kiểm tra: toàn bộ source code nằm ngay trong repo này. Tuy nhiên đây là tool nhỏ **chưa có chữ ký số (code-signing)** nên lần đầu chạy Windows hoặc phần mềm diệt virus có thể cảnh báo. Cảnh báo đó không có nghĩa là tool độc hại.

### What the app does · App làm gì

- Reads RAM / CPU / uptime of the running `RobloxPlayerBeta.exe` processes and shows them in a table.
  Đọc RAM / CPU / thời gian chạy của các tiến trình `RobloxPlayerBeta.exe` và hiện lên bảng.
- Trims Roblox's memory, sets process priority, limits CPU % and number of cores (standard Windows APIs). It only touches Roblox processes, and only when you press a button or turn AUTO on.
  Dọn RAM, đổi độ ưu tiên, giới hạn % CPU và số core của Roblox (bằng API chuẩn của Windows). Chỉ tác động tiến trình Roblox, và chỉ khi bạn bấm nút hoặc bật AUTO.
- Saves your Roblox accounts and opens them for you (several at once, auto-reopen after a crash).
  Lưu tài khoản Roblox và mở giúp bạn (nhiều acc cùng lúc, tự mở lại khi bị văng).
- Optional: sends a status report to **your own** Discord webhook.
  Tuỳ chọn: gửi báo cáo tình trạng tới Discord webhook **của chính bạn**.
- Checks GitHub for a newer version when it starts, and can update itself.
  Kiểm tra bản mới trên GitHub khi mở và có thể tự cập nhật.

### What the app does NOT do · App KHÔNG làm gì

- It does **not** read, write or inject into Roblox's memory or game files, and has no keylogger, screen capture or file scanner.
  **Không** đọc/ghi/chèn code vào bộ nhớ hay file game của Roblox; không keylogger, không chụp màn hình, không quét file của bạn.
- It does **not** add itself to Windows startup, the registry or scheduled tasks. Close the app and it is gone.
  **Không** tự thêm vào khởi động cùng Windows, registry hay scheduled task. Tắt app là hết.
- It only connects to: Roblox (sign-in and launching games), GitHub (update check/download), and the Discord webhook **you** typed in. Nothing else. There is no server of the author that collects data.
  Chỉ kết nối tới: Roblox (đăng nhập, mở game), GitHub (kiểm tra/tải cập nhật) và Discord webhook **do bạn tự nhập**. Không có máy chủ nào của tác giả thu thập dữ liệu.
- Your passwords and cookies stay on your PC, encrypted with AES-256-GCM. The key is protected by Windows DPAPI, so only your Windows user on your PC can open it. They are sent only to Roblox, to sign you in.
  Mật khẩu và cookie chỉ nằm trên máy bạn, mã hoá AES-256-GCM; khoá được bảo vệ bằng Windows DPAPI nên chỉ đúng user Windows trên máy bạn mở được. Chúng chỉ được gửi tới Roblox để đăng nhập.

### Why it asks for Administrator · Vì sao hỏi quyền Admin

To change the priority, memory and CPU limits of other processes (Roblox) reliably, Windows needs administrator rights. You will see the normal UAC prompt.
Để chỉnh độ ưu tiên, RAM và giới hạn CPU của tiến trình khác (Roblox) ổn định, Windows cần quyền Administrator. Bạn sẽ thấy hộp thoại UAC bình thường.

### Why a warning may appear · Vì sao có thể bị cảnh báo

| Warning · Cảnh báo | Reason · Lý do |
|---|---|
| Windows SmartScreen "Windows protected your PC" | The exe has no paid code-signing certificate and is new/rare. Không có chứng chỉ ký số trả phí và file còn mới/ít người tải. |
| Antivirus flags it (false positive) · Diệt virus báo nhầm | Single-file exe that unpacks its libraries, asks for admin, changes other processes' priority/memory, and can replace itself when updating — behaviours that heuristics often flag. Exe 1 file tự giải nén thư viện, xin quyền admin, chỉnh tiến trình khác và tự thay file khi cập nhật — những hành vi mà diệt virus hay báo nhầm. |

**If you see SmartScreen:** click **More info → Run anyway**.
**Nếu gặp SmartScreen:** bấm **More info → Run anyway**.

### How to check it yourself · Tự kiểm tra thế nào

1. Read the source in this repo (`App/`). The network code is in `Updater.cs`, `RobloxLauncher.cs` and `DiscordNotifier.cs`.
   Đọc source trong repo này (`App/`). Code kết nối mạng nằm ở `Updater.cs`, `RobloxLauncher.cs`, `DiscordNotifier.cs`.
2. Upload the exe to [VirusTotal](https://www.virustotal.com). A few engines may show a false positive; look at who flags it and what it is called.
   Tải exe lên [VirusTotal](https://www.virustotal.com). Có thể vài engine báo nhầm; hãy xem engine nào báo và tên gì.
3. Compare the SHA-256 shown next to the exe on the Releases page with your file: `certutil -hashfile RobloxManager_<version>_x64.exe SHA256`. The app's built-in updater also verifies this hash before replacing itself.
   So SHA-256 hiển thị cạnh file exe ở trang Releases với file của bạn: `certutil -hashfile RobloxManager_<version>_x64.exe SHA256`. Chức năng tự cập nhật trong app cũng kiểm tra mã này trước khi thay file.

### Please be aware · Lưu ý

- Opening several Roblox accounts at once is against the spirit of some game rules and Roblox can change how it works at any time. Use at your own risk, preferably with alt accounts. This tool is **not affiliated with Roblox**.
  Mở nhiều acc cùng lúc có thể không được Roblox khuyến khích và Roblox có thể đổi cách hoạt động bất cứ lúc nào. Tự chịu rủi ro, nên dùng acc phụ. Tool **không liên quan tới Roblox**.
- A Roblox cookie (`.ROBLOSECURITY`) works like a password. Never send your data folder (`accounts.dat`, `vault.key`) or your cookie to anyone.
  Cookie Roblox (`.ROBLOSECURITY`) có tác dụng như mật khẩu. Đừng gửi thư mục dữ liệu (`accounts.dat`, `vault.key`) hay cookie cho ai.
- Your Discord webhook URL is also a secret: anyone with it can post to your channel.
  Link Discord webhook cũng là bí mật: ai có link đều gửi tin vào kênh của bạn được.

---

## How to use · Hướng dẫn sử dụng

### 1. First run · Lần đầu mở

1. Run the exe and accept the Administrator (UAC) prompt.
   Chạy file exe và bấm Yes ở hộp thoại Administrator (UAC).
2. Choose a drive/folder to keep your data (settings, logs, encrypted accounts). The app creates a `RobloxManager_Data` folder there.
   Chọn ổ/thư mục để lưu dữ liệu (cài đặt, log, tài khoản đã mã hoá). App tạo thư mục `RobloxManager_Data` ở đó.
3. The language follows your system (Vietnamese / English). Change it any time in **Settings**.
   Ngôn ngữ theo hệ thống (Việt / Anh), đổi bất cứ lúc nào ở **Cài đặt**.

### 2. Overview tab · Tab Tổng quan

The app scans running Roblox windows automatically. Each account is one row (RAM, CPU, uptime, priority, status).
App tự quét các cửa sổ Roblox đang chạy. Mỗi tài khoản là một dòng (RAM, CPU, thời gian treo, độ ưu tiên, trạng thái).

| Button · Nút | What it does · Tác dụng |
|---|---|
| **Optimize All · Tối Ưu Tất Cả** | Trim RAM + cap CPU/cores for every account at once. Dọn RAM + giới hạn CPU/core cho mọi acc. |
| **Trim RAM · Trim RAM** | Release Roblox's working-set memory. Giải phóng RAM đang giữ của Roblox. |
| **AUTO** | Repeat the trim automatically (interval in Settings). Tự dọn RAM lặp lại (chu kỳ ở Cài đặt). |
| **Priority · Set** | Set the process priority (Idle … High). Đặt độ ưu tiên tiến trình. |
| **Auto-reopen on crash · Tự mở lại khi văng** | Reopen a saved account if its Roblox closes unexpectedly. Tự mở lại acc đã lưu nếu Roblox bị tắt đột ngột. |
| **Rescan · Quét lại** | Refresh the process list now. Quét lại danh sách ngay. |

Click a row to see that account's details: time running, RAM, CPU, and buttons to **optimize**, **clean RAM**, **bring to front** or **close** just that account.
Bấm vào một dòng để xem chi tiết acc đó: thời gian treo, RAM, CPU và các nút **tối ưu**, **dọn RAM**, **mở lên màn hình**, **tắt** riêng acc đó.

### 3. Accounts tab · Tab Quản lý tài khoản

1. Click **➕ New account · Acc mới**, enter username and password, then **💾 Save**.
   Bấm **➕ Acc mới**, nhập tên đăng nhập + mật khẩu, rồi **💾 Lưu acc**.
2. Optional: a **Place ID** (the game to join) and a **VIP server link**. Leave Place ID empty to just open the Roblox app.
   Tuỳ chọn: **Place ID** (game muốn vào) và **link server VIP**. Để trống Place ID thì chỉ mở app Roblox.
3. **🚀 Open account** (or double-click an account) to launch it. **🚀 Open all** launches every saved account.
   **🚀 Mở acc** (hoặc nhấp đúp vào acc) để mở. **🚀 Mở tất cả** mở mọi acc đã lưu.
4. If Roblox asks for a captcha / 2-step verification, a sign-in window opens: complete it once and it closes by itself. The session is then remembered.
   Nếu Roblox đòi captcha / xác minh 2 bước, một cửa sổ đăng nhập sẽ hiện: hoàn tất 1 lần là tự đóng, phiên được nhớ lại.
5. Advanced: instead of a password you can paste a `.ROBLOSECURITY` cookie.
   Nâng cao: thay vì mật khẩu có thể dán cookie `.ROBLOSECURITY`.

### 4. Discord report (optional) · Báo cáo Discord (tuỳ chọn)

1. In Discord: *Channel settings → Integrations → Webhooks → New Webhook → Copy URL*.
   Trong Discord: *Cài đặt kênh → Tích hợp → Webhooks → Webhook mới → Sao chép URL*.
2. Open the Discord tab, paste the URL, pick how often to report, press **💾 Save** then **🚀 Send test**.
   Mở tab Discord, dán URL, chọn tần suất báo cáo, bấm **💾 Lưu Cấu Hình** rồi **🚀 Gửi Thử Ngay**.
3. With a webhook set, the app also alerts you on crashes.
   Khi đã có webhook, app cũng báo cho bạn khi có acc bị văng.

### 5. Settings · Cài đặt

Gear icon: theme, Aurora animation (turn it off to be lighter), CPU cap per account, RAM limit per account, Auto-Trim interval, poll rate, language, data folder (open / move) and updates.
Biểu tượng bánh răng: giao diện, hiệu ứng Aurora (tắt cho nhẹ máy), giới hạn CPU mỗi acc, giới hạn RAM mỗi acc, chu kỳ Auto-Trim, tần suất quét, ngôn ngữ, thư mục dữ liệu (mở / đổi nơi lưu) và cập nhật.

### 6. Updates · Cập nhật

On start the app checks for a new version, shows **What's new**, and updates itself after you confirm. You can also use **Settings → Check for updates** and **Update history**.
Khi mở, app kiểm tra bản mới, hiện **có gì mới** và tự cập nhật sau khi bạn đồng ý. Cũng có thể dùng **Cài đặt → Kiểm tra cập nhật** và **Lịch sử cập nhật**.

### 7. Uninstall · Gỡ bỏ

Delete the exe, your `RobloxManager_Data` folder and `%AppData%\RobloxManager`. Nothing else is installed on your PC.
Xoá file exe, thư mục `RobloxManager_Data` và `%AppData%\RobloxManager`. Không có gì khác được cài vào máy.

### Troubleshooting · Gặp lỗi

| Problem · Vấn đề | Fix · Cách xử lý |
|---|---|
| Account not shown in the table · Acc không hiện trong bảng | Make sure Roblox is running, then press **Rescan**. Hãy chắc Roblox đang chạy rồi bấm **Quét lại**. |
| "Cookie expired" · "Cookie hết hạn" | Save the password for that account, or sign in again through the sign-in window. Lưu mật khẩu cho acc đó hoặc đăng nhập lại qua cửa sổ đăng nhập. |
| Accounts won't load after moving PC/Windows user · Không đọc được acc sau khi đổi máy/user Windows | Data is bound to your Windows user (DPAPI) and cannot be opened elsewhere. Add the accounts again. Dữ liệu gắn với user Windows (DPAPI), không mở được ở nơi khác. Hãy thêm lại acc. |
| Crash details · Chi tiết lỗi | See `crash.log` in your data folder and send it to the author on Discord. Xem `crash.log` trong thư mục dữ liệu và gửi cho tác giả qua Discord. |
