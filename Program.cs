using System.Globalization;
using System.Text.RegularExpressions;
using BotLogsExplorer;
using CommandLine;
using CommandLine.Text;

var parser = new Parser(with => with.HelpWriter = null);
var result = parser.ParseArguments<Options>(args);
_ = result
    .WithParsed(Run)
    .WithNotParsed(_ => DisplayHelp());


void DisplayHelp()
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

void Run(Options options)
{
    // CHECK FILE
    if (File.Exists(options.FilePath) == false)
    {
        Console.WriteLine($"FILE {options.FilePath} NOT FOUND!");
        return;
    }

    // CHECK  OPTIONS
    if (options is { FilterGroupIndex: > 0, FilterGroupValue:     null }
     || options is { FilterGroupIndex: < 0, FilterGroupValue: not null })
    {
        var text =
            """
            SPECIFY BOTH VALUES!:
                -f / --filter-by       <include regex group number> 
                -F / --filter-by-value <include regex group value>
            """;
        Console.WriteLine(text);
        return;
    }

    // ADJUST OPTIONS
    {
        if (options.Limit < 0)
            options.Limit = int.MaxValue;

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
                (true , true ) => $@" \| .*{options.Chat} (..) (....) (..) ((\/{options.Command}\S*)(?:\s(.*))?)",
                (true , false) => $@" \| .*{options.Chat} (..) (....) (..) ((\S*)(?:\s(.*))?)",
                (false, true ) =>  @" \| (.{5})"    + $@" (..) (....) (..) ((\/{options.Command}\S*)(?:\s(.*))?)",
                (false, false) => options.Include,
            };
        }
    }

    // DEBUG PATTERNS
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
    {
        if (options.Include != null)
        {
            var regex_include = new Regex(options.Include);
            linesQuery = linesQuery.Where(x => regex_include.IsMatch(x));

            if (options is { FilterGroupIndex: > 0, FilterGroupValue: not null })
            {
                var regex_filter = new Regex(options.FilterGroupValue);
                linesQuery = linesQuery.Where(x => regex_filter.IsMatch(regex_include.Match(x).Groups[options.FilterGroupIndex].Value));
            }
        }

        if (options.Exclude != null)
        {
            var regex_exclude = new Regex(options.Exclude);
            linesQuery = linesQuery.Where(x => regex_exclude.IsMatch(x) == false);
        }

        if (options.Remove != null)
        {
            var regex_remove = new Regex(options.Remove);
            linesQuery = linesQuery.Select(x => regex_remove.Replace(x, ""));
        }

        if (options.Uptime != 0)
        {
            linesQuery = linesQuery.Where(x => x.Contains("| START >>") || x.Contains("|  EXIT >>"));
        }
    }

    var linesFiltered = linesQuery.ToList();
    var countFiltered = linesFiltered.Count;
    Console.WriteLine($"{countFiltered, 8} - LINES FILTERED");

    // OUTPUT
    if (options.Uptime != 0)
    {
        var prev_month = 0;
        var year = options.Uptime;
        var botIsUp = false;
        var uptimes = new List<(TimeSpan Duration, DateTime Exit)>();
        var uptime_curr = ((TimeSpan Duration, DateTime Exit))default;
        foreach (var line in linesFiltered)
        {
            var month = int.Parse(line.Remove(2));
            if (month < prev_month) year++;
            prev_month = month;

            var timestamp = $"{year}/{line.Remove(18)}";
            var date = DateTime.ParseExact(timestamp, "yyyy/MM/dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            if (line.Contains("| START >>"))
            {
                uptime_curr.Exit = date; // store start date here, temporarily
            }
            else // | EXIT >>
            {
                uptime_curr.Duration = date - uptime_curr.Exit;
                uptime_curr.Exit = date;
                uptimes.Add(uptime_curr);
            }
        }
        if (uptime_curr.Duration == TimeSpan.Zero) // bot is up
        {
            botIsUp = true;
            var now = DateTime.Now;
            uptime_curr.Duration = now - uptime_curr.Exit;
            uptime_curr.Exit = now;
            uptimes.Add(uptime_curr);
        }
        Console.WriteLine(" ==== UPTIME ============");
        PrintUptime(botIsUp ? "CURR" : "LAST", uptime_curr.Duration);
        var periodStart = uptimes[ 0].Exit - uptimes[0].Duration;
        var periodEnd   = uptimes[^1].Exit;
        var period = periodEnd - periodStart;
        var avg = TimeSpan.FromDays(uptimes.Average(x => x.Duration.TotalDays));
        var min = TimeSpan.FromDays(uptimes.Min    (x => x.Duration.TotalDays));
        var max = TimeSpan.FromDays(uptimes.Max    (x => x.Duration.TotalDays));
        var sum = TimeSpan.FromDays(uptimes.Sum    (x => x.Duration.TotalDays));
        var sla = uptimes.Sum(x => x.Duration.TotalDays) / period.TotalDays;
        var sla_per_day = TimeSpan.FromDays(1) * sla;
        PrintUptime("AVG", avg);
        PrintUptime("MIN", min);
        PrintUptime("MAX", max);
        PrintUptime("SUM", sum);
        Console.WriteLine(" ==== SLA ===============");
        Console.WriteLine($"  ALL TIME, %: {sla * 100}%");
        Console.WriteLine($"  ALL TIME, H: {sla_per_day:hh\\:mm\\:ss} (per day)");

        var today = DateTime.Today;
        var day_4W_ago = today.AddDays(-28);
        var last28days = new TimeSpan[28]; // from today to 4w ago
        foreach (var (duration, end) in uptimes.SkipWhile(x => x.Exit.Date < day_4W_ago))
        {
            var       start = end - duration;
            var day = start; // init with exact time!
            while (true)
            {
                var i = (today - day.Date).Days;
                if (i >= 28) continue;

                if (day.Date == end.Date)
                {
                    var time = end - day;
                    last28days[i] += time;
                    break;
                }

                var nextDay = day.Date.AddDays(1);
                last28days[i] += nextDay - day;
                day = nextDay; // set to 00:00 on iterations
            }
        }

        var week1 = TimeSpan.FromDays(last28days.Skip( 0).Take(7).Average(x => x.TotalDays));
        var week2 = TimeSpan.FromDays(last28days.Skip( 7).Take(7).Average(x => x.TotalDays));
        var week3 = TimeSpan.FromDays(last28days.Skip(14).Take(7).Average(x => x.TotalDays));
        var week4 = TimeSpan.FromDays(last28days.Skip(21).Take(7).Average(x => x.TotalDays));
        Console.WriteLine(               $"        TODAY: {last28days[0]:hh\\:mm\\:ss}");
        Console.WriteLine(               $"    YESTERDAY: {last28days[1]:hh\\:mm\\:ss}");
        Console.WriteLine($"{today.AddDays(-2),13:MM/dd}: {last28days[2]:hh\\:mm\\:ss}");
        Console.WriteLine($"{today.AddDays(-3),13:MM/dd}: {last28days[3]:hh\\:mm\\:ss}");
        Console.WriteLine($"{today.AddDays(-4),13:MM/dd}: {last28days[4]:hh\\:mm\\:ss}");
        Console.WriteLine($"{today.AddDays(-5),13:MM/dd}: {last28days[5]:hh\\:mm\\:ss}");
        Console.WriteLine($"{today.AddDays(-6),13:MM/dd}: {last28days[6]:hh\\:mm\\:ss}");
        Console.WriteLine(               $"THIS WEEK AVG: {week1:hh\\:mm\\:ss}");
        Console.WriteLine(               $"LAST WEEK AVG: {week2:hh\\:mm\\:ss}");
        Console.WriteLine(               $"  2W  AGO AVG: {week3:hh\\:mm\\:ss}");
        Console.WriteLine(               $"  3W  AGO AVG: {week4:hh\\:mm\\:ss}");

        void PrintUptime(string prefix, TimeSpan duration)
        {
            var days =                   duration.Days;
            var time = TimeSpan.FromDays(duration.TotalDays - days);
            var days_ed = days == 1 ? "," : "s,";
            Console.WriteLine($"{prefix,4}: {days,4} day{days_ed,-2} {time:hh\\:mm\\:ss}");
        }
    }
    else if (options is { Include: not null, GroupIndex: > 0 })
    {
        var regex = new Regex(options.Include);
        var groups = linesFiltered
            .GroupBy(x => regex.Match(x).Groups[options.GroupIndex].Value)
            .OrderByDescending(x => x.Count()).ToList();

        Console.WriteLine($"{groups.Count, 8} - LINES DISTINCT");
        Console.WriteLine("\n   COUNT PERCENT EVENT");
        foreach (var group in groups.Skip(options.Skip).Take(options.Limit))
        {
            var count = group.Count();
            var percent = Math.Round(100F * count / countFiltered, 2);
            Console.WriteLine($"{count,8} {percent,6}% {group.Key}");
        }
    }
    else if (options.TimeTable)
    {
        var regex = new Regex(@"^(\d\d\/\d\d \d\d:\d\d:\d\d\.\d\d\d) \| ");
        var dateTimes = linesFiltered
            .Select(x => regex.Match(x))
            .Where(x => x.Success)
            .Select(x => DateTime.ParseExact(x.Groups[1].Value, "MM/dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .ToList();
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
        foreach (var line in linesFiltered.Skip(options.Skip).Take(options.Limit))
        {
            Console.WriteLine(line);
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