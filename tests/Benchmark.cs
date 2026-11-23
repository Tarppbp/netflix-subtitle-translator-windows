using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

internal static class Benchmark
{
    [MTAThread]
    private static void Main(string[] args)
    {
        if (args.Length < 1 || args.Length > 2) return;
        string model = args.Length == 2 ? args[1] : LocalEngine.DefaultModel;
        string[] cues = {
            "すみません、田中さん。新宿まで一緒に行っていただけますか？",
            "えっ、今さらそんなこと言う？",
            "いいんだ。気にしないで。",
            "お母さんには、まだ言わないでくれ。",
            "先輩、例の件ですが、もう少し待っていただけませんか。",
            "別に怒ってないよ。心配しただけ。",
            "来ないって言ったのは、君のほうだろ。",
            "あの子、無理して笑ってたんだね。",
            "手を貸してくれる？\nもちろん。何をすればいい？",
            "お疲れさま。今日はもう上がっていいよ。"
        };
        using (var output = new StreamWriter(args[0]))
        using (var engine = new LocalEngine())
        {
            output.AutoFlush = true;
            var latencies = new System.Collections.Generic.List<double>();
            for (int i = 0; i < cues.Length; i++)
            {
                var watch = Stopwatch.StartNew();
                try
                {
                    var previous = i > 0 && i < 4 ? new[]{cues[i - 1]} : new string[0];
                    var result = engine.Translate(cues[i], previous, model, CancellationToken.None)
                        .GetAwaiter().GetResult();
                    double ms = watch.Elapsed.TotalMilliseconds;
                    if (i != 0) latencies.Add(ms);
                    output.WriteLine("{0:F0} ms | {1} | {2}", ms, cues[i].Replace("\n", " / "), result.Replace("\n", " / "));
                }
                catch (Exception error) { output.WriteLine("ERROR " + error.Message); }
            }
            var sorted = latencies.OrderBy(x => x).ToArray();
            if (sorted.Length > 0)
                output.WriteLine("Second through tenth requests: n={0}, p50={1:F0} ms, p95={2:F0} ms",
                    sorted.Length, sorted[(int)Math.Ceiling(sorted.Length * .5) - 1],
                    sorted[(int)Math.Ceiling(sorted.Length * .95) - 1]);
        }
    }
}
