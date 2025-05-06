using System.Globalization;
using System.Text.RegularExpressions;
using CommandLine;

namespace BotLogsExplorer
{
    public class Options
    {
        [Option('i', "include", Required = false, HelpText = "Filter logs that INCLUDE pattern.")]
        public string? Include { get; set; }

        [Option('e', "exclude", Required = false, HelpText = "Filter logs that EXCLUDE pattern.")]
        public string? Exclude { get; set; }

        [Option('r', "ignore", Required = false, HelpText = "Pattern that should be removed, e.g. \"@xyz_bot(?<=\\S)\".")]
        public string? Ignore { get; set; }

        [Option('g', "group", Required = false, Default = -1, HelpText = "Group logs by an index of --include regex capture group.")]
        public int Group { get; set; }

        [Value(0, Default = "log.txt", MetaName = "FILE", HelpText = "Logs file path.")]
        public string FilePath { get; set; } = null!;

        [Option('c', "chat", Required = false, Default = -1, HelpText = "Filter logs by chat id (last 4 numbers).")]
        public int Chat { get; set; }
        
        [Option('m', "command", Required = false, HelpText = "Filter logs by command (without slash).")]
        public string? Command { get; set; }

        [Option('d', "debug", Required = false)]
        public bool Debug { get; set; }

        [Option('l', "limit", Required = false, Default = -1, HelpText = "Limit output to a max lines value.")]
        public int Limit { get; set; }

        [Option('t', "time", Required = false, HelpText = "Visualize data as a timetable.")]
        public bool Timetable { get; set; }

        [Option('o', "time-offset", Required = false, HelpText = "Time offset in hours relative to bot timezone.")]
        public int TimeOffset { get; set; }
    }

    internal static class Program
    {
        public const string AUTO = @"(\[auto,\s*(\S*)\]\s)?";

        static void Main(string[] args)
        {
            var options = Parser.Default.ParseArguments<Options>(args).Value;
            if (options.Chat > 0 && options.Command != null) options.Include = $@"{options.Chat}] >> {AUTO}((\/{options.Command}\S*)(?:\s(.*))?)";
            else if (options.Command != null)                options.Include =               $@"] >> {AUTO}((\/{options.Command}\S*)(?:\s(.*))?)";
            else if (options.Chat > 0)                       options.Include = $@"{options.Chat}] >> {AUTO}((\S*).*)";

            if (options.Limit < 0)
            {
                options.Limit = int.MaxValue;
            }

            if (options.Debug)
            {
                Console.WriteLine($"INCLUDE: {options.Include}");
                Console.WriteLine($"EXCLUDE: {options.Exclude}");
                Console.WriteLine();
            }

            var lines = File.ReadAllLines(options.FilePath);
            Console.WriteLine($"{lines.Length, 8} - LINES TOTAL");

            var linesQuery = lines.AsEnumerable();
            if (options.Include != null)
            {
                var regex = new Regex(options.Include);
                linesQuery = linesQuery.Where(x => regex.IsMatch(x));
            }
            if (options.Exclude != null)
            {
                var regex = new Regex(options.Exclude);
                linesQuery = linesQuery.Where(x => !regex.IsMatch(x));
            }
            if (options.Ignore != null)
            {
                var regex = new Regex(options.Ignore);
                linesQuery = linesQuery.Select(x => regex.Replace(x, ""));
            }

            var linesFiltered = linesQuery.ToList();
            var countFiltered = linesFiltered.Count;
            Console.WriteLine($"{countFiltered, 8} - LINES FILTERED");

            if (options is { Include: not null, Group: > 0 })
            {
                var regex = new Regex(options.Include);
                var groups = linesFiltered
                    .GroupBy(x => regex.Match(x).Groups[options.Group].Value)
                    .OrderByDescending(x => x.Count()).ToList();

                Console.WriteLine($"{groups.Count, 8} - LINES DISTINCT");
                Console.WriteLine("\n   COUNT PERCENT EVENT");
                foreach (var group in groups.Take(options.Limit))
                {
                    var count = group.Count();
                    var percent = Math.Round(100F * count / countFiltered, 2);
                    Console.WriteLine($"{count,8} {percent,6}% {group.Key}");
                }
            }
            else if (options.Timetable)
            {
                var regex = new Regex(@"\[(.+) \| \.\.\d+\]");
                var dateTimes = linesFiltered.Select(x => DateTime.ParseExact(regex.Match(x).Groups[1].Value, "MM/dd HH:mm:ss.fff", CultureInfo.InvariantCulture)).ToList();
                var byH = dateTimes.GroupBy(x => x.Hour     ).ToDictionary(x => x.Key, x => x.Count());
                var byD = dateTimes.GroupBy(x => x.DayOfWeek).ToDictionary(x => x.Key, x => x.Count());
                var byM = dateTimes.GroupBy(x => x.Month    ).ToDictionary(x => x.Key, x => x.Count());

                var k = 100F / countFiltered;

                Console.WriteLine("\nBY HOUR");
                for (var i = 0; i < 24; i++)
                {
                    var x = byH.GetValueOrDefault(i, 0);
                    var hour = (24 + i + options.TimeOffset) % 24;
                    Console.WriteLine($"{hour,2}:00 - {hour+1,2}:00 {x,8} {new string('=', (int)(k * x))}");
                }

                Console.WriteLine("\nBY WEEK DAY");
                for (var i = 0; i < 7; i++)
                {
                    var x = byD.GetValueOrDefault((DayOfWeek)i, 0);
                    Console.WriteLine($"{(DayOfWeek)i,13} {x,8} {new string('=', (int)(k * x))}");
                }

                Console.WriteLine("\nBY MONTH");
                for (var i = 1; i < 13; i++)
                {
                    var x = byM.GetValueOrDefault(i, 0);
                    var month = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(i);
                    Console.WriteLine($"{month,13} {x,8} {new string('=', (int)(k * x))}");
                }
            }
            else
            {
                Console.WriteLine();
                foreach (var line in linesFiltered.Take(options.Limit))
                {
                    Console.WriteLine(line);
                }
            }
        }
    }
}