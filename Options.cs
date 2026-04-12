using CommandLine;

namespace BotLogsExplorer;

public class Options
{
    [Value(0, MetaName = "FILE",         HelpText = "Logs file path.", Default = "./Log/log.txt")]
    public string? FilePath         { get; set; }
    [Option('i',  "include",             HelpText = "Filter lines with given pattern.")]
    public string? Include          { get; set; }
    [Option('e',  "exclude",             HelpText = "Filter lines w/o  given pattern.")]
    public string? Exclude          { get; set; }
    [Option('r',  "remove",              HelpText = "Pattern to remove from lines, e.g. \"@xyz_bot(?<=\\S)\".")]
    public string? Remove           { get; set; }
    [Option('g',  "group-by",            HelpText = "Group lines by regex group index of --include pattern.", Default = -1)]
    public int     GroupIndex       { get; set; }
    [Option('f',  "filter-by",           HelpText = "Filter lines by regex group index of --include pattern.", Default = -1)]
    public int     FilterGroupIndex { get; set; }
    [Option('F',  "filter-by-value",     HelpText = "Filter lines by value of regex group which index is supplied via --filter-by.")]
    public string? FilterGroupValue { get; set; }
    [Option('c',  "chat",                HelpText = "Filter lines by chat id (last 1-5 digits).", Default = -1)]
    public int     Chat             { get; set; }
    [Option('C',  "command",             HelpText = "Filter lines by command (w/o slash).")]
    public string? Command          { get; set; }
    [Option('T',  "type",                HelpText = "Filter lines by type: [C]OMMAND, [A]UTO, CALL[B]ACK, [I]NLINE, [E]VENT.")]
    public string? Type             { get; set; }
    [Option('E',  "event",               HelpText = "Filter lines by event: START, SAVE, ADMIN, EXIT etc.")]
    public string? Event            { get; set; }
    [Option('d',  "debug",               HelpText = "Debug --include and --exculde patterns.")]
    public bool    Debug            { get; set; }
    [Option('s',  "skip",                HelpText = "Number of lines to skip.", Default = 0)]
    public int     Skip             { get; set; }
    [Option('l',  "limit",               HelpText = "Max number of lines to output.", Default = -1)]
    public int     Limit            { get; set; }
    [Option('t',  "time",                HelpText = "Visualize data as a timetable.")]
    public bool    TimeTable        { get; set; }
    [Option('o',  "time-offset",         HelpText = "Time offset in hours relative to bot timezone.")]
    public int     TimeOffset       { get; set; }
}