namespace VersionDiff;

public static class Program
{
    private const string Usage = @"Usage:
  VersionDiff patches [--old <dir>] [--new <dir>] [--mod <dll>]... [--out <file>]
  VersionDiff data <old dump dir> <new dump dir> [--out <file>]";

    public static int Main(string[] args)
    {
        if (args.Length == 0) return Fail();
        var rest = args.Skip(1).ToArray();
        return args[0] switch
        {
            "patches" => PatchDiff.Run(rest),
            "data" => DataDiff.Run(rest),
            _ => Fail(),
        };
    }

    internal static int Fail(string message = null)
    {
        if (message != null) Console.Error.WriteLine(message);
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
