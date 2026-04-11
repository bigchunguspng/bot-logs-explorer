using System.Globalization;
using System.Text.RegularExpressions;
using CommandLine;
using CommandLine.Text;

namespace BotLogsExplorer;

public class Options
{
    [Value(0, Default = "./Log/log.txt", MetaName = "FILE", HelpText = "Logs file path.")]
    public string FilePath { get; set; } = null!;

    [Option('i', "include", HelpText = "Filter lines with given pattern.")]
    public string? Include { get; set; }

    [Option('e', "exclude", HelpText = "Filter lines w/o  given pattern.")]
    public string? Exclude { get; set; }

    [Option('r', "remove", HelpText = "Pattern to remove from lines, e.g. \"@xyz_bot(?<=\\S)\".")]
    public string? Remove { get; set; }

    [Option('g', "group", Default = -1, HelpText = "Group lines by regex group index of --include pattern.")]
    public int GroupIndex { get; set; }

    [Option('G', "filter-group", HelpText = "Filter lines by value of regex group supplied via --group.")]
    public string? GroupValue { get; set; }

    [Option('c', "chat", Default = -1, HelpText = "Filter lines by chat id (last 1-5 digits).")]
    public int Chat { get; set; }

    [Option('C', "command", HelpText = "Filter lines by command (w/o slash).")]
    public string? Command { get; set; }

    [Option('T', "type", HelpText = "Filter lines by type: [C]OMMAND, [A]UTO, CALL[B]ACK, [I]NLINE, [E]VENT.")]
    public string? Type { get; set; }

    [Option('E', "event", HelpText = "Filter lines by event: START, SAVE, ADMIN, EXIT etc.")]
    public string? Event { get; set; }

    [Option('d', "debug", HelpText = "Debug --include and --exculde patterns.")]
    public bool Debug { get; set; }

    [Option('l', "limit", Default = -1, HelpText = "Max number of lines to output.")]
    public int Limit { get; set; }

    [Option('t', "time", HelpText = "Visualize data as a timetable.")]
    public bool Timetable { get; set; }

    [Option('o', "time-offset", HelpText = "Time offset in hours relative to bot timezone.")]
    public int TimeOffset { get; set; }
}

internal static class Program
{
    private static void Main(string[] args)
    {
        var parser = new Parser(with => with.HelpWriter = null);
        var result = parser.ParseArguments<Options>(args);
        result
            .WithParsed(Run)
            .WithNotParsed(_ => DisplayHelp(result));
    }

    private static void DisplayHelp<T>(ParserResult<T> result)
    {
        var helpText = HelpText.AutoBuild(result, help =>
        {
            help.Copyright = string.Empty;
            help.AdditionalNewLineAfterOption = false;
            help.MaximumDisplayWidth = 120;
            return HelpText.DefaultParsingErrorsHandler(result, help);
        });
        Console.WriteLine(helpText);
    }

    private static void Run(Options options)
    {
        // CHECK FILE
        if (File.Exists(options.FilePath) == false)
        {
            Console.WriteLine($"FILE {options.FilePath} NOT FOUND!");
            return;
        }

        // ADJUST OPTIONS
        if (options.Event != null)
        {
            options.Include = $@" \| .*{options.Event} .. (.*)";
        }
        else if (options.Type != null)
        {
            var chat = options.Chat > 0 ? $".*{options.Chat}" : ".....";
            options.Include = options.Type.ToUpper() switch
            {
                "C" => $@" \| ({chat}) -> (....) (..) \/([^ []+)(?: |\[N\])?(.*)", //     [C]OMMAND
                "A" => $@" \| ({chat}) <- (....) (..) \@(\S+) (....) ?(.*)",       //     [A]UTO
                "B" => $@" \| ({chat}) -> (....) \*(.*)",                          // CALL[B]ACK
                "I" => $@" \| ({chat}) -- (....) \@\S+ ?(.*)",                     //     [I]NLINE
                "E" =>  @" \| (.....) >> (.*)",                                    // BOT [E]VENTS
                _ => null,
            };

            if (options.Include == null)
            {
                var text =
                    $"""
                     WRONG TYPE {options.Type}
                     VALID TYPES:
                         C - COMMAND
                         A - AUTO
                         B - CALLBACK
                         I - INLINE
                         E - BOT EVENTS
                     """;
                Console.WriteLine(text);
                return;
            }
        }
        else
        {
            options.Include = (options.Chat > 0, options.Command != null) switch
            {
                (true , true ) => $@" \| .*{options.Chat} .. .... .. ((\/{options.Command}\S*)(?:\s(.*))?)",
                (true , false) => $@" \| .*{options.Chat} .. .... .. ((\S*)(?:\s(.*))?)",
                (false, true ) =>  @" \| .{5}"      + $@" .. .... .. ((\/{options.Command}\S*)(?:\s(.*))?)",
                (false, false) => options.Include,
            };
        }

        if (options.Limit < 0)
            options.Limit = int.MaxValue;

        // DEBUG
        if (options.Debug)
        {
            Console.WriteLine($"INCLUDE: {options.Include}");
            Console.WriteLine($"EXCLUDE: {options.Exclude}");
            Console.WriteLine();
        }

        // READ FILE
        var lines = File.ReadAllLines(options.FilePath);
        Console.WriteLine($"{lines.Length, 8} - LINES TOTAL");

        // FILTER LINES
        var linesQuery = lines.AsEnumerable();
        if (options.Include != null)
        {
            var regex = new Regex(options.Include);
            linesQuery = linesQuery.Where(x => regex.IsMatch(x));

            if (options is { GroupIndex: > 0, GroupValue: not null })
            {
                var regex_2 = new Regex(options.GroupValue);
                linesQuery = linesQuery.Where(x => regex_2.IsMatch(regex.Match(x).Groups[options.GroupIndex].Value));
            }
        }
        if (options.Exclude != null)
        {
            var regex = new Regex(options.Exclude);
            linesQuery = linesQuery.Where(x => !regex.IsMatch(x));
        }
        if (options.Remove != null)
        {
            var regex = new Regex(options.Remove);
            linesQuery = linesQuery.Select(x => regex.Replace(x, ""));
        }

        var linesFiltered = linesQuery.ToList();
        var countFiltered = linesFiltered.Count;
        Console.WriteLine($"{countFiltered, 8} - LINES FILTERED");

        // OUTPUT
        if (options is { Include: not null, GroupIndex: > 0, GroupValue: null })
        {
            var regex = new Regex(options.Include);
            var groups = linesFiltered
                .GroupBy(x => regex.Match(x).Groups[options.GroupIndex].Value)
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
            var regex = new Regex(@"^(.+?) \| ");
            var dateTimes = linesFiltered.Select(x =>
            {
                var time = regex.Match(x).Groups[1].Value;
                return DateTime.ParseExact(time, "MM/dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            }).ToList();
            var byH = dateTimes.GroupBy(x => x.Hour     ).ToDictionary(x => x.Key, x => x.Count());
            var byD = dateTimes.GroupBy(x => x.DayOfWeek).ToDictionary(x => x.Key, x => x.Count());
            var byM = dateTimes.GroupBy(x => x.Month    ).ToDictionary(x => x.Key, x => x.Count());

            var k = 100F / countFiltered;

            Console.WriteLine("\nBY HOUR");
            for (var i = 0; i < 24; i++)
            {
                var x = byH.GetValueOrDefault(i, 0);
                var hour = (24 + i + options.TimeOffset) % 24;
                Console.WriteLine($"{hour,2}:00 - {hour + 1,2}:00 {x,8} {new string('=', (int)(k * x))}");
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

/* ==== Templates

COMMAND:                   ->         /
10/14 21:43:13.280 | 12345 -> OK   -T /stickers
10/14 21:42:13.828 |  CHAT -> OK   PT /toplarg@bot text
10/14 21:42:13.828 |  CHAT -> MAN  -P /im implode

AUTO:                      <-         @
10/14 21:42:13.828 |  CHAT <- OK   -P @MEME  25% /mememm!
10/14 21:42:13.828 |  CHAT <- OK   PP @MEME 150% /toplarg text
10/14 21:42:13.828 |  CHAT <- FAIL -V @PIPE 48XA /scale 0.5
10/14 21:42:13.828 |  CHAT <- FAIL -V @AUTO 48XA /pipe scale 0.5 > /nuke > /damn 15
10/14 21:42:13.828 |  CHAT <- OK   -T @TEXT  20%

CALLBACK:                  ->      *
10/14 21:42:13.828 |  CHAT -> OK   *bi - 8 10

INLINE:                    --      @
10/14 21:42:13.828 |  USER -- OK   @bot funny
10/14 21:42:13.828 |  USER -- OK   @bot a sfx

BOT EVENTS                 >>
10/14 21:42:13.828 | START >> @bot | Adidas
10/14 21:42:13.828 |  SAVE >> CHATS 1280 | PACKS   15 | SAVE    3 | DROP    2
10/14 21:42:13.828 | ADMIN >> /w lol kek
10/14 21:42:13.828 |  EXIT >> @bot | Adidas

*/