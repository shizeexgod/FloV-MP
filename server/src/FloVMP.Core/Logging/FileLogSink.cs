using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace FloVMP.Core.Logging;

/// <summary>
/// Файловый синк: JSON Lines в
/// <c>&lt;root&gt;/&lt;category&gt;/&lt;yyyy-MM-dd&gt;.jsonl</c>.
///
/// Продюсер (<see cref="Write"/>) только кладёт запись в очередь и сразу
/// возвращается — гейм-тик не ждёт диск. Фоновый воркер пишет пачками.
/// Файловый синк всегда доступен как fallback: даже когда появится БД-синк,
/// этот остаётся дублирующим, чтобы ничего не терять.
///
/// При переполнении очереди (капасити исчерпан) новая запись отбрасывается
/// (счётчик <see cref="_dropped"/>) — логи не должны положить сервер.
/// </summary>
public sealed class FileLogSink : ILogSink
{
    private readonly string _root;
    private readonly Channel<LogEntry> _chan;
    private readonly Task _worker;
    private readonly Func<DateTime> _utcNow;

    private long _pending;   // поставлено в очередь, ещё не записано на диск
    private long _dropped;   // отброшено из-за переполнения

    public long Dropped => Interlocked.Read(ref _dropped);

    private static readonly JsonSerializerOptions JsonOpts = new();
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public FileLogSink(string root, int capacity = 10_000, Func<DateTime>? utcNow = null)
    {
        _root = root;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        Directory.CreateDirectory(_root);

        _chan = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

        _worker = Task.Run(RunAsync);
    }

    public void Write(LogEntry entry)
    {
        // TryWrite не ждёт: при полной очереди вернёт false — считаем как drop.
        if (_chan.Writer.TryWrite(entry))
            Interlocked.Increment(ref _pending);
        else
            Interlocked.Increment(ref _dropped);
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        // ждём, пока всё поставленное в очередь окажется на диске
        var spins = 0;
        while (Interlocked.Read(ref _pending) > 0)
        {
            await Task.Delay(10, ct).ConfigureAwait(false);
            if (++spins > 2000) break; // ~20 c — не виснем навсегда
        }
    }

    public async ValueTask DisposeAsync()
    {
        _chan.Writer.TryComplete();
        try { await _worker.ConfigureAwait(false); } catch { /* воркер логирует сам */ }
    }

    private async Task RunAsync()
    {
        var batch = new List<LogEntry>(256);
        try
        {
            while (await _chan.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                batch.Clear();
                while (batch.Count < 256 && _chan.Reader.TryRead(out var e))
                    batch.Add(e);

                WriteBatch(batch);
                Interlocked.Add(ref _pending, -batch.Count);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FloV:MP] FileLogSink worker упал: {ex}");
        }

        // добить остаток при завершении канала
        batch.Clear();
        while (_chan.Reader.TryRead(out var e)) batch.Add(e);
        if (batch.Count > 0)
        {
            WriteBatch(batch);
            Interlocked.Add(ref _pending, -batch.Count);
        }
    }

    private void WriteBatch(List<LogEntry> batch)
    {
        foreach (var grp in batch.GroupBy(FileFor))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(grp.Key)!);
                var sb = new StringBuilder();
                foreach (var e in grp)
                    sb.Append(JsonSerializer.Serialize(e, JsonOpts)).Append('\n');
                File.AppendAllText(grp.Key, sb.ToString(), Utf8NoBom);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FloV:MP] лог не записан ({grp.Key}): {ex.Message}");
            }
        }
    }

    private string FileFor(LogEntry e)
    {
        var date = DateTime.TryParse(
            e.TsUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var dt) ? dt : _utcNow();
        var cat = string.IsNullOrWhiteSpace(e.Category) ? LogCategory.System : e.Category;
        return Path.Combine(_root, cat, $"{date:yyyy-MM-dd}.jsonl");
    }
}
