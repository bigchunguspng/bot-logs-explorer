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

        [Option('r', "ignore", Required = false, HelpText = "Text that should be ignored. E.g. @bot_username.")]
        public string? Ignore { get; set; }

        [Option('g', "group", Required = false, Default = -1, HelpText = "Group logs by an index of --include regex capture group.")]
        public int Group { get; set; }

        [Value(0, Default = "log*.txt", MetaName = "FILE", HelpText = "Logs file path.")]
        public string FilePath { get; set; } = null!;

        [Option('c', "chat", Required = false, Default = -1, HelpText = "Filter logs by chat id (last 4 numbers).")]
        public int Chat { get; set; }
        
        [Option('m', "command", Required = false, HelpText = "Filter logs by command (without slash).")]
        public string? Command { get; set; }
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
                var ignore = options.Ignore;
                linesQuery = linesQuery.Select(x => x.Replace(ignore, ""));
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
                foreach (var group in groups)
                {
                    var count = group.Count();
                    var percent = Math.Round(100F * count / countFiltered, 2);
                    Console.WriteLine($"{count,8} {percent,6}% {group.Key}");
                }
            }
            else
            {
                foreach (var line in linesFiltered)
                {
                    Console.WriteLine(line);
                }
            }
        }
    }
}