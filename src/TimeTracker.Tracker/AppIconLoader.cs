namespace TimeTracker.Tracker;

internal static class AppIconLoader
{
    public static Icon Load()
    {
        var fromExe = TryExtractAssociated();
        if (fromExe is not null)
        {
            return fromExe;
        }

        var icoPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(icoPath))
        {
            return new Icon(icoPath);
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private static Icon? TryExtractAssociated()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            return null;
        }
    }
}
