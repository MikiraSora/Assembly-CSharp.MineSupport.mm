using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using MineSupport;

internal static class Program
{
    private static int Main()
    {
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "MineSupport.LogTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Environment.CurrentDirectory = directory;

            const string message = "[Mine] chart rejected: path=test.ma2, record=7, type=NMTAP, tail=!M, reason=lowercase required";
            PatchLog.Error(message);

            var path = Path.Combine(directory, PatchLog.FilePath);
            string text = null;
            for (var i = 0; i < 80; i++)
            {
                if (File.Exists(path))
                {
                    text = File.ReadAllText(path, Encoding.UTF8);
                    if (text.Contains(message))
                        break;
                }
                Thread.Sleep(25);
            }

            Require(text != null && text.Contains(message), "Release Error entry was not written");
            Require(text.Contains("[ERROR]"), "ERROR level is missing");
            Require(text.Contains("[Thread: "), "thread id is missing");
            Require(Regex.IsMatch(text, @"^\[\d{4}-\d{2}-\d{2}T.*Z\]\[Thread: "), "UTC ISO timestamp is missing");

            var bytes = File.ReadAllBytes(path);
            Require(bytes.Length < 3 || bytes[0] != 0xEF || bytes[1] != 0xBB || bytes[2] != 0xBF,
                "log unexpectedly contains a UTF-8 BOM");

            System.Console.WriteLine("MineSupport.LogTests: PASS");
            System.Console.WriteLine(path);
            return 0;
        }
        catch (Exception exception)
        {
            System.Console.Error.WriteLine("MineSupport.LogTests: FAIL");
            System.Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
