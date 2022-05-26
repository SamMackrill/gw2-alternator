using NLog.Config;
using NLog.Targets;

namespace guildwars2.tools.alternator;

public static class Logging
{
    /// <summary>
    /// Create Custom Logger using parameters passed.
    /// </summary>
    /// <param name="name">Name of file.</param>
    /// <param name="entryLayout">Give "" if you want just message. If omitted will switch to full log parameters.</param>
    /// <param name="fileLayout">Filename only. No extension or file paths accepted.</param>
    /// <param name="absoluteFilePath">If you want to save the log file to different path than application default log path, specify the path here.</param>
    /// <returns>New instance of NLog logger completely isolated from default instance if any</returns>
    public static Logger CreateCustomLogger(
        string name = "CustomLog",
        string entryLayout = "${ date:format=dd.MM.yyyy HH\\:mm\\:ss.fff} thread[${threadid}] ${logger} (${level:uppercase=true}): ${message}. ${exception:format=ToString}",
        string fileLayout = "logs/{0}.${{shortdate}}.log",
        string? absoluteFilePath = null
        )
    {
        var factory = new LogFactory();
        var target = new FileTarget
        {
            Name = name,
            FileName = absoluteFilePath == null
                ? string.Format(fileLayout, name)
                : string.Format(Path.Combine(absoluteFilePath, fileLayout), name),
            Layout = entryLayout == "" 
                ? "${message}. ${exception:format=ToString}" 
                : entryLayout,
        };

        var config = new LoggingConfiguration();
        config.AddTarget(name, target);

        var ruleInfo = new LoggingRule("*", NLog.LogLevel.Trace, target);

        config.LoggingRules.Add(ruleInfo);

        factory.Configuration = config;

        return factory.GetCurrentClassLogger();
    }

}