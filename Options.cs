using CommandLine;

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
    public bool TimeTable { get; set; }

    [Option('o', "time-offset", HelpText = "Time offset in hours relative to bot timezone.")]
    public int TimeOffset { get; set; }
}