using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

internal static class Probe
{
    [MTAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 1) return;
        using (var log = new StreamWriter(args[0]))
        {
            log.AutoFlush = true;
            NetflixReader.DebugTree = true;
            NetflixReader.Trace = message => log.WriteLine(message);
            for (int i = 0; i < 32; i++)
            {
                var watch = Stopwatch.StartNew();
                try
                {
                    var cue = NetflixReader.Read();
                    log.WriteLine(cue == null ? "No player" : String.Format(
                        "{0} ms, bounds {1}, cue lines {2}, kana {3}",
                        watch.ElapsedMilliseconds, cue.Bounds,
                        cue.Text.Length == 0 ? 0 : cue.Text.Split('\n').Length,
                        System.Text.RegularExpressions.Regex.IsMatch(cue.Text, "[ぁ-ヺ]")));
                }
                catch (Exception error) { log.WriteLine(error.GetType().Name + " " + error.Message); }
                Thread.Sleep(300);
            }
        }
    }
}
