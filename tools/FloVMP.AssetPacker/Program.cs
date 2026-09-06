using System;
using System.IO;
using FloVMP.Core.Assets;
using Packer = FloVMP.Core.Assets.AssetPacker;

namespace FloVMP.AssetPacker.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("  FloV:MP FastDL & Asset Packager CLI (v1.0.0)  ");
        Console.WriteLine("=================================================");

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        try
        {
            return command switch
            {
                "pack" => RunPack(args),
                "verify" => RunVerify(args),
                "compress" => RunCompress(args),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL] {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static int RunPack(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Использование: flovmp-packer pack <каталог_ассетов> [файл_манифеста]");
            return 1;
        }

        var sourceDir = Path.GetFullPath(args[1]);
        var outputFile = args.Length >= 3 ? Path.GetFullPath(args[2]) : Path.Combine(sourceDir, "manifest.json");

        Console.WriteLine($"[Pack] Сканирование: {sourceDir}");
        var manifest = Packer.ScanDirectory(sourceDir);

        Console.WriteLine($"[Pack] Найдено файлов: {manifest.TotalFiles}, общий размер: {manifest.TotalBytes / 1024.0 / 1024.0:F2} МБ");

        var json = manifest.ToJson();
        File.WriteAllText(outputFile, json);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Pack] Успешно сохранён манифест -> {outputFile}");
        Console.ResetColor();
        return 0;
    }

    private static int RunVerify(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Использование: flovmp-packer verify <каталог_ассетов> [файл_манифеста]");
            return 1;
        }

        var sourceDir = Path.GetFullPath(args[1]);
        var manifestFile = args.Length >= 3 ? Path.GetFullPath(args[2]) : Path.Combine(sourceDir, "manifest.json");

        if (!File.Exists(manifestFile))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Verify] Файл манифеста не найден: {manifestFile}");
            Console.ResetColor();
            return 1;
        }

        var json = File.ReadAllText(manifestFile);
        var manifest = AssetManifest.FromJson(json);
        if (manifest == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[Verify] Ошибка парсинга JSON-манифеста");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"[Verify] Проверка {sourceDir} по манифесту (v{manifest.Version}, {manifest.TotalFiles} файлов)...");
        var result = Packer.VerifyDirectory(sourceDir, manifest);

        if (result.IsValid)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Verify] ОК! Все файлы соответствуют манифесту.");
            if (result.UntrackedFiles.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Verify] Неотслеживаемых файлов: {result.UntrackedFiles.Count}");
            }
            Console.ResetColor();
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Verify] ОШИБКА ЦЕЛОСТНОСТИ!");
        foreach (var missing in result.MissingFiles)
        {
            Console.WriteLine($"  - Отсутствует: {missing}");
        }
        foreach (var mismatch in result.HashMismatches)
        {
            Console.WriteLine($"  - Хэш не совпадает: {mismatch.Path} (Ожидался: {mismatch.ExpectedSha256[..8]}..., факт: {mismatch.ActualSha256[..8]}...)");
        }
        Console.ResetColor();
        return 2;
    }

    private static int RunCompress(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Использование: flovmp-packer compress <исходный_файл> <целевой_файл.gz>");
            return 1;
        }

        var source = Path.GetFullPath(args[1]);
        var target = Path.GetFullPath(args[2]);

        if (!File.Exists(source))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Файл не найден: {source}");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"[Compress] Сжатие: {source} -> {target}");
        Packer.GzipCompressFile(source, target);

        var origSize = new FileInfo(source).Length;
        var compSize = new FileInfo(target).Length;
        var ratio = 100.0 - (compSize * 100.0 / Math.Max(1, origSize));

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Compress] Завершено! {origSize} байт -> {compSize} байт (-{ratio:F1}%)");
        Console.ResetColor();
        return 0;
    }

    private static int UnknownCommand(string cmd)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Неизвестная команда: {cmd}");
        Console.ResetColor();
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine();
        Console.WriteLine("Команды:");
        Console.WriteLine("  pack <каталог> [манифест.json]    - Сканировать файлы и сгенерировать FastDL манифест");
        Console.WriteLine("  verify <каталог> [манифест.json]  - Проверить целостность и хеши локальных файлов");
        Console.WriteLine("  compress <файл> <целевой.gz>      - Оптимально сжать файл для быстрой раздачи по сети");
        Console.WriteLine();
    }
}
