using System;
using System.Diagnostics;
using System.Net;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal sealed class DesktopControl : IDisposable
{
    private const string Origin = "chrome-extension://nphopjbohdkdjapgjmgnpbpmmcgmdimo";
    private readonly HttpListener listener = new HttpListener();
    private volatile byte[] settings = Encoding.UTF8.GetBytes("{\"enabled\":false}");
    private long lastPoll;
    private volatile string statistics = "";
    private volatile string playerStatus = "";
    internal bool IsListening { get { return listener.IsListening; } }
    internal string Statistics { get { return statistics; } }
    internal string PlayerStatus { get { return playerStatus; } }
    internal bool Connected
    {
        get
        {
            long poll = Interlocked.Read(ref lastPoll);
            return poll != 0 && Stopwatch.GetTimestamp() - poll < 2 * Stopwatch.Frequency;
        }
    }

    internal DesktopControl()
    {
        listener.Prefixes.Add("http://127.0.0.1:17435/");
        try { listener.Start(); }
        catch (HttpListenerException) { return; }
        catch (InvalidOperationException) { return; }
        Listen();
    }

    internal void Publish(bool enabled, Preferences p)
    {
        var snapshot = new
        {
            enabled, chineseOnly = p.ChineseOnly, model = p.Model,
            font = p.Font, color = p.Color, size = p.Size, bottom = p.Bottom, stroke = p.Stroke,
            background = p.Opacity, context = p.Context, followSubtitle = p.FollowSubtitle
        };
        settings = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(snapshot));
    }

    private async void Listen()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync().ConfigureAwait(false); }
            catch (HttpListenerException) { return; }
            catch (ObjectDisposedException) { return; }
            try
            {
                using (var response = context.Response)
                {
                    var request = context.Request;
                    string origin = request.Headers["Origin"];
                    if (!request.IsLocal || request.HttpMethod != "GET" || request.Url.AbsolutePath != "/settings" ||
                        request.Headers["X-Netflix-Subtitle-Control"] != "1" || (origin != null && origin != Origin))
                    {
                        response.StatusCode = 404;
                        response.ContentLength64 = 0;
                        continue;
                    }
                    var parts = (request.Headers["X-Netflix-Subtitle-Stats"] ?? "").Split(',');
                    int read, shown, hits, latency;
                    if (parts.Length == 4 && ParseCount(parts[0], out read) && ParseCount(parts[1], out shown) &&
                        ParseCount(parts[2], out hits) && ParseCount(parts[3], out latency))
                        statistics = String.Format("已读 {0} 条 · 已显示 {1} 条 · 缓存命中 {2} 次 · 平均 {3} ms",
                            read, shown, hits, latency);
                    playerStatus = PlayerStatusText(request.Headers["X-Netflix-Subtitle-State"]);
                    Interlocked.Exchange(ref lastPoll, Stopwatch.GetTimestamp());
                    byte[] snapshot = settings;
                    response.ContentType = "application/json; charset=utf-8";
                    response.Headers["Cache-Control"] = "no-store";
                    response.Close(snapshot, true);
                }
            }
            catch (System.IO.IOException) {}
            catch (HttpListenerException) {}
            catch (ObjectDisposedException) {}
        }
    }

    private static bool ParseCount(string text, out int value)
    {
        return Int32.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;
    }

    private static string PlayerStatusText(string state)
    {
        switch (state)
        {
            case "waiting": return "等待 Netflix 日文字幕";
            case "translating": return "正在翻译当前台词";
            case "error": return "本句翻译失败 · 请检查本地模型";
            case "displaying": return "正在显示中文字幕";
            default: return "";
        }
    }

    public void Dispose() { listener.Close(); }
}
