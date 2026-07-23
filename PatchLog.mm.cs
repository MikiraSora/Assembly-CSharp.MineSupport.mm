// MineSupport.PatchLog — 统一记录 Mine 解析、资源和运行时边界错误。
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace MineSupport
{
    internal static class PatchLog
    {
        public const string FilePath = "dpMineSupport.log";

        private const int MaxBatchSize = 128;
        private static readonly ConcurrentQueue<LogEntry> Queue = new ConcurrentQueue<LogEntry>();
        private static readonly ConcurrentDictionary<string, byte> ErrorKeys =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        private static readonly AutoResetEvent QueueSignal = new AutoResetEvent(false);
        private static readonly Thread WorkerThread;
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static bool Enabled { get; set; } = true;

        static PatchLog()
        {
            WorkerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "MineSupport PatchLog Writer"
            };
            WorkerThread.Start();
        }

        [Conditional("DEBUG")]
        public static void WriteLine(string message)
        {
            Enqueue(message, false);
        }

        public static void Error(string message)
        {
            var text = message ?? string.Empty;
            Enqueue(text, true);
            try
            {
                UnityEngine.Debug.LogError(text);
            }
            catch
            {
            }
        }

        public static void ErrorOnce(string key, string message)
        {
            var normalizedKey = string.IsNullOrEmpty(key) ? message ?? string.Empty : key;
            if (ErrorKeys.TryAdd(normalizedKey, 0))
                Error(message);
        }

        private static void Enqueue(string message, bool isError)
        {
            if (!Enabled)
                return;

            Queue.Enqueue(new LogEntry(DateTime.UtcNow, Thread.CurrentThread.ManagedThreadId, message ?? string.Empty, isError));
            QueueSignal.Set();
        }

        private static void WorkerLoop()
        {
            try
            {
                File.Delete(FilePath);
            }
            catch
            {
            }

            var batch = new List<LogEntry>(MaxBatchSize);
            while (true)
            {
                QueueSignal.WaitOne(100);
                DrainQueue(batch);
            }
        }

        private static void DrainQueue(List<LogEntry> batch)
        {
            while (true)
            {
                while (batch.Count < MaxBatchSize && Queue.TryDequeue(out var entry))
                    batch.Add(entry);

                if (batch.Count == 0)
                    return;

                FlushBatch(batch);
            }
        }

        private static void FlushBatch(List<LogEntry> batch)
        {
            try
            {
                var text = new StringBuilder(batch.Count * 160);
                for (var i = 0; i < batch.Count; i++)
                {
                    var entry = batch[i];
                    text.Append('[');
                    text.Append(entry.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
                    text.Append("][Thread: ");
                    text.Append(entry.ThreadId);
                    text.Append("][");
                    text.Append(entry.IsError ? "ERROR" : "INFO");
                    text.Append("] ");
                    text.Append(entry.Message);
                    text.Append(Environment.NewLine);
                }

                File.AppendAllText(FilePath, text.ToString(), Utf8NoBom);
            }
            catch
            {
            }

            batch.Clear();
        }

        private readonly struct LogEntry
        {
            internal LogEntry(DateTime timestampUtc, int threadId, string message, bool isError)
            {
                TimestampUtc = timestampUtc;
                ThreadId = threadId;
                Message = message;
                IsError = isError;
            }

            internal DateTime TimestampUtc { get; }
            internal int ThreadId { get; }
            internal string Message { get; }
            internal bool IsError { get; }
        }
    }
}
