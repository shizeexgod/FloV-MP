using System;
using System.IO;
using System.Text;

class Program {
    static void Main(string[] args) {
        var baseDir = AppContext.BaseDirectory;
        string? clientDir = null;
        var dir = baseDir;
        for (int i = 0; i < 6; i++) {
            var candidate = Path.Combine(dir, "runtime", "client");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "altv-client.dll"))) {
                clientDir = candidate;
                break;
            }
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        if (clientDir == null) {
            clientDir = @"C:\FloV-MP\runtime\client";
        }

        Console.WriteLine($"[patcher] Target client dir: {clientDir}");

        var altvExe = Path.Combine(clientDir, "altv.exe");
        var flovmpExe = Path.Combine(clientDir, "flovmp.exe");
        if (File.Exists(altvExe) && (!File.Exists(flovmpExe) || new FileInfo(altvExe).LastWriteTimeUtc > new FileInfo(flovmpExe).LastWriteTimeUtc)) {
            File.Copy(altvExe, flovmpExe, true);
            Console.WriteLine($"[patcher] Created flovmp.exe from altv.exe");
        }

        PatchFile(Path.Combine(clientDir, "altv-client.dll"));
        PatchFile(Path.Combine(clientDir, "altv.exe"));
        PatchFile(Path.Combine(clientDir, "flovmp.exe"));
    }

    static void PatchFile(string path) {
        if (!File.Exists(path)) return;
        var bytes = File.ReadAllBytes(path);
        
        var orig1 = Encoding.ASCII.GetBytes("https://api.alt-mp.com");
        var repl1 = Encoding.ASCII.GetBytes("http://127.0.0.1:9988\0\0");
        
        var orig2 = Encoding.ASCII.GetBytes("http://194.104.146.133");
        var repl2 = Encoding.ASCII.GetBytes("http://127.0.0.1:9988\0\0");
        
        int patches = Replace(bytes, orig1, repl1);
        patches += Replace(bytes, orig2, repl2);
        
        if (patches > 0) {
            File.WriteAllBytes(path, bytes);
            Console.WriteLine($"[patcher] Patched {Path.GetFileName(path)}: {patches} replacements");
        } else {
            Console.WriteLine($"[patcher] No patches needed for {Path.GetFileName(path)}");
        }
    }

    static int Replace(byte[] bytes, byte[] search, byte[] repl) {
        int count = 0;
        for (int i = 0; i <= bytes.Length - search.Length; i++) {
            bool match = true;
            for (int j = 0; j < search.Length; j++) {
                if (bytes[i + j] != search[j]) {
                    match = false;
                    break;
                }
            }
            if (match) {
                for (int j = 0; j < repl.Length; j++) {
                    bytes[i + j] = repl[j];
                }
                count++;
            }
        }
        return count;
    }
}
