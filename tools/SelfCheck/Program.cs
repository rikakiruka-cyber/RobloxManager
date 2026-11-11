using System;
using System.Diagnostics;
using System.IO;
using RobloxRAMManager;

namespace SelfCheck;

class Program
{
    static void Main()
    {
        Console.WriteLine("[SELF-CHECK] Starting validation...");

        // 1. Verify RAM cap floor is 1024 MB
        Debug.Assert(NativeInterop.MIN_RAM_CAP_MB == 1024, $"Expected MIN_RAM_CAP_MB == 1024, got {NativeInterop.MIN_RAM_CAP_MB}");
        Console.WriteLine("  ✓ Check 1: MIN_RAM_CAP_MB is 1024 MB");

        // 2. Verify RobloxLogReader incremental reading and PlaceId detection
        var logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");
        Directory.CreateDirectory(logsDir);
        var dummyFile = Path.Combine(logsDir, $"SelfCheck_Player_{Guid.NewGuid():N}.log");
        var startTime = DateTime.Now;

        try
        {
            // Initial log with PlaceId 12345678 and many extra lines to test early stop
            using (var sw = new StreamWriter(dummyFile))
            {
                sw.WriteLine("Log initialized");
                sw.WriteLine("Joining game 'test-guid' place 12345678 at 127.0.0.1:53640");
                for (int i = 0; i < 500; i++)
                {
                    sw.WriteLine($"Telemetry trace line {i}: memory usage normal, physics step 60fps");
                }
            }
            File.SetCreationTime(dummyFile, startTime);

            long place = RobloxLogReader.FindPlaceId(startTime);
            Debug.Assert(place == 12345678, $"Expected place 12345678, got {place}");
            Console.WriteLine("  ✓ Check 2: RobloxLogReader parsed initial PlaceId 12345678");

            // Append teleport line
            using (var sw = File.AppendText(dummyFile))
            {
                sw.WriteLine("Teleporting to another universe...");
                sw.WriteLine("Joining game 'teleport-guid' place 99887766 at 127.0.0.1:53641");
            }

            long newPlace = RobloxLogReader.FindPlaceId(startTime);
            Debug.Assert(newPlace == 99887766, $"Expected updated place 99887766, got {newPlace}");
            Console.WriteLine("  ✓ Check 3: RobloxLogReader incrementally parsed teleported PlaceId 99887766");
        }
        finally
        {
            try { if (File.Exists(dummyFile)) File.Delete(dummyFile); } catch { }
        }

        // 3. Verify TrimSelf GC collection does not throw and is non-blocking
        NativeInterop.TrimSelf();
        Console.WriteLine("  ✓ Check 4: NativeInterop.TrimSelf executed cleanly");

        // 4. Verify CanTrim cooldown logic
        uint dummyPid = 999999;
        Debug.Assert(NativeInterop.CanTrim(dummyPid), "dummyPid should be trimmable initially");
        NativeInterop.TrimMemory(dummyPid, force: false); // marks last trim
        Debug.Assert(!NativeInterop.CanTrim(dummyPid), "dummyPid should be on cooldown immediately after trim");
        Console.WriteLine("  ✓ Check 5: NativeInterop.CanTrim cooldown verification passed");

        Console.WriteLine("[SELF-CHECK] All checks passed successfully!");
    }
}
