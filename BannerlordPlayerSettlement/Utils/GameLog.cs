using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using TaleWorlds.Library;

using TaleWorldsDebug = TaleWorlds.Library.Debug;

namespace BannerlordPlayerSettlement.Utils
{
    internal sealed class GameLog
    {
        private const string BeginMultiLine = @"=======================================================================================================================\";
        private const string BeginMultiLineDebug = @"===================================================   D E B U G   =====================================================\";
        private const string EndMultiLine = @"=======================================================================================================================/";

        public readonly string Module;
        public readonly string LogDir;
        public readonly string LogFile;
        public readonly string LogPath;

        private bool _lastMsgWasMultiLine = false;

        private TextWriter Writer { get; set; }

        public void Info(string text) => Print(text, Colours.White);
        public void Info(List<string> text) => Print(text, Colours.White);
        public void Debug(string text) => Print(text, Colours.Magenta, true);
        public void Debug(List<string> text) => Print(text, Colours.Magenta, true);
        public void NotifyBad(Exception e)
        {
            LogManager.EventTracer.Trace(e.Message, 2);

            TaleWorldsDebug.PrintError(e.Message, e.StackTrace);
            TaleWorldsDebug.WriteDebugLineOnScreen(e.ToString());
            TaleWorldsDebug.SetCrashReportCustomString(e.Message);
            TaleWorldsDebug.SetCrashReportCustomStack(e.StackTrace);

            Print(e.Message, Colours.Red, onlyDisplay: true);
            ToFile(new List<string> { e.Message, e.StackTrace });
        }
        public void NotifyBad(string text) => Print(text, Colours.Red);
        public void NotifyBad(List<string> text) => Print(text, Colours.Red);
        public void NotifyNeutral(string text) => Print(text, Colours.SkyBlue);
        public void NotifyNeutral(List<string> text) => Print(text, Colours.SkyBlue);
        public void NotifyGood(string text) => Print(text, Colours.ForestGreen);
        public void NotifyGood(List<string> text) => Print(text, Colours.ForestGreen);

        public void Print(string text, Color color, bool isDebug = false, bool onlyDisplay = false)
        {
            InformationManager.DisplayMessage(new InformationMessage(text, color));

            if (!onlyDisplay)
            {
                ToFile(text, isDebug);
            }
        }

        public void Print(List<string> lines, Color color, bool isDebug = false, bool onlyDisplay = false)
        {
            foreach (string text in lines)
            {
                InformationManager.DisplayMessage(new InformationMessage(text, color));
            }

            if (!onlyDisplay)
            {
                ToFile(lines, isDebug);
            }
        }

        public /* async */ void ToFile(string line, bool isDebug = false)
        {
            if (Writer is null)
            {
                return;
            }

            _lastMsgWasMultiLine = false;
            Writer.WriteLine(isDebug ? $">> {line}" : line);
            //await Writer.FlushAsync();
            Writer.Flush();
        }

        public /* async */ void ToFile(List<string> lines, bool isDebug = false)
        {
            if (Writer is null || lines.Count == 0)
            {
                return;
            }

            if (lines.Count == 1)
            {
                ToFile(lines[0], isDebug);
                return;
            }

            if (!_lastMsgWasMultiLine)
            {
                Writer.WriteLine(isDebug ? BeginMultiLineDebug : BeginMultiLine);
            }

            _lastMsgWasMultiLine = true;

            foreach (string line in lines)
            {
                Writer.WriteLine(line);
            }

            Writer.WriteLine(EndMultiLine);
            // await Writer.FlushAsync();
            Writer.Flush();
        }

        public GameLog(string moduleName, bool truncate = false, string? logName = null)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                throw new ArgumentException($"{nameof(moduleName)}: string cannot be null or empty");
            }

            var fullPath = Common.PlatformFileHelper.GetFileFullPath(new PlatformFilePath(
                            new PlatformDirectoryPath(PlatformFileType.Application, "logs"),
                            string.IsNullOrEmpty(logName) ? $"{moduleName}.log" : $"{moduleName}.{logName}.log")
            );

            Module = $"{moduleName}.{GetType().Name}";
            LogDir = Path.GetDirectoryName(fullPath);
            LogFile = Path.GetFileName(fullPath);
            LogPath = fullPath;

            if (!Directory.Exists(LogDir))
            {
                Directory.CreateDirectory(LogDir);
            }

            var existed = File.Exists(LogPath);

            try
            {
                Writer = TextWriter.Synchronized(new StreamWriter(LogPath, !truncate, Encoding.UTF8, (1 << 15)));
            }
            catch (Exception e)
            {
                Console.WriteLine($"================================  EXCEPTION  ================================");
                Console.WriteLine($"{Module}: Failed to create StreamWriter!");
                Console.WriteLine($"Path: {LogPath}");
                Console.WriteLine($"Truncate: {truncate}");
                Console.WriteLine($"Preexisting Path: {existed}");
                Console.WriteLine($"Exception Information:");
                Console.WriteLine($"{e}");
                Console.WriteLine($"=============================================================================");
                throw;
            }

            Writer.NewLine = "\n";

            var msg = new List<string>
            {
                $"{Module} created at: {DateTimeOffset.Now:yyyy/MM/dd H:mm zzz}",
            };

            if (existed && !truncate)
            {
                Writer.WriteLine("\n");
                msg.Add("NOTE: Any prior log messages in this file may have no relation to this session.");
            }

            ToFile(msg, true);
        }
    }
}
