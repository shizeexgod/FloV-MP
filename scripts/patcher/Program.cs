using System;
using System.IO;
using System.Text;

class Program {
    static void Main(string[] args) {
        PatchFile("../../runtime/client/altv-client.dll");
        PatchFile("../../runtime/client/altv.exe");
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
            Console.WriteLine($"Patched {path}: {patches} replacements");
        } else {
            Console.WriteLine($"No patches needed for {path}");
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
