using System;
using System.Threading;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace IotFleet;

public class Helpers {
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    static readonly NLog.Layouts.Layout _localLayout = "${longdate}|${uppercase:${level}}|{logger}|${message} ${onexception}";
    static readonly NLog.Layouts.Layout _mqttLayout = "${message} ${onexception}";

    public static string LoadEnvOrDie(string envVariable, string defaultValue = "") {
        if (string.IsNullOrEmpty(envVariable)) { throw new ArgumentException("Requested ENV variable must be an non-empty string.", nameof(envVariable)); }

        var loadedVariable = Environment.GetEnvironmentVariable(envVariable);
        if (string.IsNullOrEmpty(loadedVariable) && string.IsNullOrEmpty(defaultValue)) {
            _log.Fatal($"Evironment variable {envVariable} is not in the environment. Application will exit after a few seconds.");
            Thread.Sleep(20000); // <-- Preventing restart loops in docker containers, so the user at least could see the error messages.
            Environment.Exit(-1);
        }
        else if (string.IsNullOrEmpty(loadedVariable)) {
            _log.Warn($"Evironment variable {envVariable} is not provided. Using default value {defaultValue}.");
            loadedVariable = defaultValue;
        }
        else {
            _log.Warn($"Evironment variable {envVariable} is set to {loadedVariable}.");
        }

        return loadedVariable;
    }

    public static void AddFileOutputToLogger(LoggingConfiguration configuration, string filename = "logs/ApplicationLog.txt") {
        var logfile = new FileTarget(filename);
        logfile.Layout = _localLayout;
        logfile.MaxArchiveFiles = 30;
        logfile.ArchiveDateFormat = "yyyy-MM";
        logfile.ArchiveNumbering = ArchiveNumberingMode.Date;
        logfile.ArchiveEvery = FileArchivePeriod.Month;
        logfile.FileName = filename;
        configuration.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);
    }
}
