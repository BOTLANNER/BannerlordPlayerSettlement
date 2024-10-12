using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using TaleWorlds.CampaignSystem;

namespace BannerlordPlayerSettlement.Utils
{
    internal static class LogManager
    {
        internal static bool EnableTracer { get; set; } = false;

        internal static GameLog Log = new(Main.Name, truncate: true, logName: "debug");

        internal static class EventTracer
        {
            private static readonly ConcurrentDictionary<string, bool> _stackTraceMap = new ConcurrentDictionary<string, bool>();

            [MethodImpl(MethodImplOptions.NoInlining)]
            internal static void Trace(string extraInfo, int framesToSkip = 1) => Trace(new List<string> { extraInfo }, framesToSkip + 1);

            [MethodImpl(MethodImplOptions.NoInlining)]
            internal static void Trace(List<string>? extraInfo = null, int framesToSkip = 1)
            {
                if (!EnableTracer)
                {
                    return;
                }

                var st = new StackTrace(framesToSkip, true);
                var frames = st.GetFrames();
                var evtMethod = frames[0].GetMethod();

                var msg = new List<string>
                {
                    $"Code Event Invoked: {evtMethod.DeclaringType}.{evtMethod.Name}",
                    $"Real Timestamp:     {DateTime.Now:MM/dd H:mm:ss.fff}",
                };

                if (Campaign.Current is not null)
                {
                    msg.AddRange(new List<string>
                    {
                        $"Campaign Time:              {(CampaignTime.Now)}",
                        $"Campaign Current Time:      {(Campaign.CurrentTime)}"
                    });
                }

                var stStr = st.ToString();

                if (stStr.Length > 2)
                {
                    // ensure we're using Unix-style EOLs in the stack trace & remove extra newline at end
                    stStr = stStr.Replace("\r\n", "\n");
                    stStr = stStr.Remove(stStr.Length - 1, 1);

                    // only show a distinct stack trace once per event traced
                    if (_stackTraceMap.TryAdd(stStr, true))
                    {
                        msg.AddRange(new List<string>
                        {
                            string.Empty,
                            "Stack Trace:",
                            stStr,
                        });
                    }
                }

                if (extraInfo is not null && extraInfo.Count > 0)
                {
                    msg.AddRange(new List<string>
                    {
                        string.Empty,
                        "Extra Information:",
                    });

                    if (extraInfo.Count > 1)
                    {
                        msg.Add(string.Empty);
                    }

                    msg.AddRange(extraInfo);
                }

                Log.ToFile(msg, true);
            }
        }
    }
}
