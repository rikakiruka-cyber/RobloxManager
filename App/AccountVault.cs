// Roblox RAM Manager — Copyright (c) 2026 RikaSoora — https://discord.com/users/1374334753242873908
// Licensed under the terms in LICENSE. Keep this notice. / Giữ nguyên dòng ghi công này.

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RobloxRAMManager;

public sealed class AccountEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Cookie { get; set; } = "";      // cookie phiên đã lưu (tự cập nhật sau khi đăng nhập)
    public long PlaceId { get; set; }
    public long LastPlaceId { get; set; }          // game gần nhất acc này đang chơi (tự phát hiện từ log Roblox)
    public bool AutoReopen { get; set; } = true;
    public string VipLink { get; set; } = "";       // link server VIP (tuỳ chọn)
    /// <summary>Id của client/bootstrapper muốn dùng để mở acc này (xem ClientKind). Rỗng = để hệ thống tự chọn (mặc định Windows).</summary>
    public string PreferredClientId { get; set; } = "";
}

/// <summary>
/// Két lưu tài khoản. Toàn bộ danh sách (kể cả username) mã hoá AES-256-GCM.
/// Khoá 256-bit ngẫu nhiên được bọc bằng Windows DPAPI (chỉ đúng user Windows này mở được).
/// </summary>
public static class AccountVault
{
    public static List<AccountEntry> Accounts { get; private set; } = new();
    /// <summary>true nếu có file tài khoản nhưng không giải mã được (khác user/máy) → không ghi đè.</summary>
    public static bool LoadFailed { get; private set; }

    private static string DataPath => DataStore.FilePath("accounts.dat");
    private static string KeyPath => DataStore.FilePath("vault.key");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RobloxManager.vault.v1");
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RMV1");
    private static readonly object _io = new();
    private static string BakPath => DataPath + ".bak";

    private static byte[]? GetKey(bool create)
    {
        try
        {
            if (File.Exists(KeyPath))
                return ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), Entropy, DataProtectionScope.CurrentUser);
            if (!create) return null;
            var k = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(KeyPath, ProtectedData.Protect(k, Entropy, DataProtectionScope.CurrentUser));
            return k;
        }
        catch { return null; }
    }

    /// <summary>Giải mã 1 file két. Trả về null nếu file không có / hỏng / sai khoá.</summary>
    private static List<AccountEntry>? TryRead(string path, byte[]? key)
    {
        try
        {
            if (key == null || !File.Exists(path)) return null;
            var blob = File.ReadAllBytes(path);
            if (blob.Length < 4 + 12 + 16 || !blob.AsSpan(0, 4).SequenceEqual(Magic)) return null;

            var nonce = blob.AsSpan(4, 12);
            var tag = blob.AsSpan(16, 16);
            var cipher = blob.AsSpan(32);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, cipher, tag, plain, Magic);
            return JsonSerializer.Deserialize<List<AccountEntry>>(plain) ?? new();
        }
        catch { return null; }
    }

    public static bool Load()
    {
        lock (_io)
        {
            Accounts = new();
            LoadFailed = false;
            try
            {
                if (!File.Exists(DataPath) && !File.Exists(BakPath)) return true;
                var key = GetKey(false);
                // File chính hỏng (mất điện giữa chừng...) → thử bản sao lưu gần nhất
                var list = TryRead(DataPath, key) ?? TryRead(BakPath, key);
                if (list == null) { LoadFailed = true; return false; }
                Accounts = list;
                return true;
            }
            catch { LoadFailed = true; Accounts = new(); return false; }
        }
    }

    public static bool Save()
    {
        if (LoadFailed) return false;
        lock (_io)
        {
            try
            {
                var key = GetKey(true);
                if (key == null) return false;
                var plain = JsonSerializer.SerializeToUtf8Bytes(Accounts);
                var nonce = RandomNumberGenerator.GetBytes(12);
                var tag = new byte[16];
                var cipher = new byte[plain.Length];
                using (var aes = new AesGcm(key, 16))
                    aes.Encrypt(nonce, plain, cipher, tag, Magic);

                using var ms = new MemoryStream();
                ms.Write(Magic); ms.Write(nonce); ms.Write(tag); ms.Write(cipher);
                var tmp = DataPath + ".tmp";
                File.WriteAllBytes(tmp, ms.ToArray());

                // Giữ lại bản trước đó (chỉ khi nó còn đọc được) để lỡ ghi hỏng vẫn khôi phục được danh sách acc
                try { if (TryRead(DataPath, key) != null) File.Copy(DataPath, BakPath, true); } catch { }

                File.Move(tmp, DataPath, true);
                CryptographicOperations.ZeroMemory(plain);
                return true;
            }
            catch { return false; }
        }
    }
}
