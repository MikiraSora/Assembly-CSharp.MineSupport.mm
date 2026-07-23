using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Manager;
using MineSupport;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            System.Console.Error.WriteLine("Usage: MineSupport.ChartGenerator <input.ma2> <output.ma2>");
            return 1;
        }

        try
        {
            var input = Path.GetFullPath(args[0]);
            var output = Path.GetFullPath(args[1]);
            var lines = File.ReadAllLines(input, Encoding.UTF8);
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var mineCount = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var record = new MA2Record(line);
                if (!record.getType().isValid() || record.getType().getCategory() != Ma2Category.MA2_Note)
                    continue;

                var fields = new List<string>(line.Split('\t'));
                var tailIndex = record.getType().getParamNum();
                if (fields.Count == tailIndex)
                {
                    fields.Add("!m");
                }
                else if (fields.Count == tailIndex + 1)
                {
                    if (fields[tailIndex].Contains("!m"))
                        throw new InvalidDataException($"line {i + 1} already contains a Mine marker");
                    fields[tailIndex] = "!m" + fields[tailIndex];
                }
                else
                {
                    throw new InvalidDataException(
                        $"line {i + 1} has {fields.Count} fields; expected {tailIndex} or {tailIndex + 1}");
                }

                MineTailParseResult parsed;
                string reason;
                if (!MineTailParser.TryParse(fields[tailIndex], out parsed, out reason) || !parsed.IsMine)
                    throw new InvalidDataException($"line {i + 1} generated an invalid Mine tail: {reason}");

                lines[i] = string.Join("\t", fields);
                var type = record.getType().getEnumName();
                counts[type] = counts.TryGetValue(type, out var count) ? count + 1 : 1;
                mineCount++;
            }

            var directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllLines(output, lines, new UTF8Encoding(false));

            System.Console.WriteLine($"MineSupport.ChartGenerator: PASS ({mineCount} note records)");
            foreach (var pair in counts)
                System.Console.WriteLine($"  {pair.Key}: {pair.Value}");
            System.Console.WriteLine(output);
            return 0;
        }
        catch (Exception exception)
        {
            System.Console.Error.WriteLine("MineSupport.ChartGenerator: FAIL");
            System.Console.Error.WriteLine(exception);
            return 2;
        }
    }
}
