using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

internal sealed class Preferences
{
    public string Model { get; set; }
    public string SourceLanguageCode { get; set; }
    public string TargetLanguageCode { get; set; }
    public string Font { get; set; }
    public string Color { get; set; }
    public int Size { get; set; }
    public int Bottom { get; set; }
    public int Stroke { get; set; }
    public int Opacity { get; set; }
    public bool ChineseOnly { get; set; }
    public bool Context { get; set; }
    public bool FollowSubtitle { get; set; }
    public int TypographyVersion { get; set; }
    internal bool SameLanguage { get { return SourceLanguageCode == TargetLanguageCode; } }

    public Preferences()
    {
        Model = LocalEngine.DefaultModel;
        SourceLanguageCode = SubtitleLanguage.DefaultSource;
        TargetLanguageCode = SubtitleLanguage.DefaultTarget;
        Font = SubtitleOverlay.DefaultFont;
        Color = "#FFFFFF";
        Size = 34;
        Bottom = 24;
        Stroke = 2;
        Context = true;
        FollowSubtitle = true;
    }
}

internal sealed class ControlPanel : Window
{
    private static readonly string Data = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".runtime");
    private static readonly string SettingsFile = Path.Combine(Data, "settings.json");
    private static readonly string LogFile = Path.Combine(Data, "app.log");
    private readonly LocalEngine engine = new LocalEngine();
    private readonly SubtitleOverlay overlay = new SubtitleOverlay();
    private readonly Preferences config = new Preferences();
    private readonly Queue<string> previous = new Queue<string>();
    private readonly Dictionary<string, string> cache = new Dictionary<string, string>();
    private readonly Queue<string> cacheKeys = new Queue<string>();
    private const int CacheCapacity = 12000;
    private static readonly TimeSpan ActiveInterval = TimeSpan.FromMilliseconds(55);
    private static readonly TimeSpan IdleInterval = TimeSpan.FromMilliseconds(220);
    private readonly Queue<double> latencies = new Queue<double>();
    private readonly Queue<string> events = new Queue<string>();
    private readonly TextBlock readout = new TextBlock();
    private readonly TextBlock performance = new TextBlock();
    private readonly ComboBox models = new ComboBox();
    private readonly Button toggle = new Button();
    private readonly TextBox log = new TextBox();
    private readonly DispatcherTimer ticker;
    private readonly PlaybackWatch playbackWatch;
    private readonly DesktopControl desktopControl;
    private int pendingRead;
    private bool readAfterCurrent;
    private CancellationTokenSource requestCancel;
    private CancellationTokenSource warmupCancel;
    private Task warmup;
    private string recentModel;
    private DateTime lastModelResponse;
    private bool reading, translating, enabled, quitting, loading;
    private bool wasForeground;
    private bool telemetry;
    private int generation, received, hits, successes, overlays, eventReads;
    private string episode, source = "", chinese = "", error = "";
    private string lastError = "", lastShown = "";
    private string readyKey;
    private double readyModelLatency;
    private long verifiedAfter;
    private Rect lastBounds, lastCaptionBounds;
    private DateTime nextTry;
    private DateTime cueArrived;
    private DateTime playbackSampleTime, nextTelemetry;
    private double? playbackPosition;
    private double lastVisibleLatency;
    private NetflixReader.Sample current;

    [STAThread]
    public static void Main(string[] args)
    {
        bool created;
        using (var single = new Mutex(true, "Local\\NetflixJapaneseSubtitleTranslator", out created))
        using (var show = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\NetflixJapaneseSubtitleTranslator.Show"))
        {
            if (!created) { show.Set(); return; }
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var panel = new ControlPanel();
            panel.telemetry = args.Contains("--test-telemetry");
            app.MainWindow = panel;
            var subscription = ThreadPool.RegisterWaitForSingleObject(show, delegate
            {
                panel.Dispatcher.BeginInvoke(new Action(panel.OpenPanel));
            }, null, Timeout.Infinite, false);
            if (args.Contains("--start")) panel.SetEnabled(true);
            panel.Show();
            try { app.Run(); }
            finally { subscription.Unregister(null); }
        }
    }

    public ControlPanel()
    {
        Load();
        desktopControl = new DesktopControl();
        desktopControl.Publish(false, config);
        Title = "Netflix 字幕翻译";
        Width = 504;
        Height = 790;
        MinWidth = 450;
        MinHeight = 530;
        ShowInTaskbar = true;
        Background = Brush("#F3F6FA");
        Foreground = Brush("#18273C");
        FontFamily = new FontFamily("Segoe UI Variable, Microsoft YaHei UI");
        FontSize = 14;
        Resources.Add(typeof(Button), ButtonStyle());

        var body = new StackPanel { Margin = new Thickness(22) };
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Content = scroll;
        body.Children.Add(new TextBlock { Text = "Netflix 字幕翻译", FontSize = 28,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 0, 0, 14) });
        var playback = new StackPanel();
        readout.TextWrapping = TextWrapping.Wrap;
        readout.FontWeight = FontWeights.SemiBold;
        playback.Children.Add(readout);
        performance.Foreground = Brush("#4D5D73");
        performance.TextWrapping = TextWrapping.Wrap;
        performance.Margin = new Thickness(0, 5, 0, 12);
        playback.Children.Add(performance);
        toggle.Background = Brush("#215FCC");
        toggle.Foreground = Brushes.White;
        toggle.BorderBrush = Brush("#215FCC");
        toggle.Margin = new Thickness(0, 0, 0, 12);
        toggle.Click += delegate { SetEnabled(!enabled); };
        playback.Children.Add(toggle);
        body.Children.Add(Card(playback));

        var settings = new StackPanel();
        settings.Children.Add(new TextBlock {Text = "字幕设置", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 9)});
        var form = new Grid();
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(124) });
        form.ColumnDefinitions.Add(new ColumnDefinition());
        settings.Children.Add(form);
        body.Children.Add(Card(settings));
        AddRow(form, "翻译引擎", Choice(new[] {"Ollama · 本机"}, "Ollama · 本机"));
        models.Items.Add(config.Model);
        models.SelectedItem = config.Model;
        models.SelectionChanged += delegate
        {
            var model = models.SelectedItem as string;
            if (loading || model == null || model == config.Model) return;
            config.Model = model;
            if (requestCancel != null) requestCancel.Cancel();
            RestartCue();
            if (enabled) WarmModel();
            Save();
        };
        var modelRow = new DockPanel();
        var reload = new Button { Content = "刷新模型", Padding = new Thickness(8, 3, 8, 3) };
        reload.Click += async delegate { await RefreshModels(); };
        DockPanel.SetDock(reload, Dock.Right);
        modelRow.Children.Add(reload);
        modelRow.Children.Add(models);
        AddRow(form, "AI 模型", modelRow);
        AddRow(form, "源字幕语言", LanguageChoice(config.SourceLanguageCode, code => config.SourceLanguageCode = code));
        AddRow(form, "目标字幕语言", LanguageChoice(config.TargetLanguageCode, code => config.TargetLanguageCode = code));
        var chineseOnly = new CheckBox { Content = "只显示译文", IsChecked = config.ChineseOnly };
        chineseOnly.Click += delegate
        {
            config.ChineseOnly = chineseOnly.IsChecked == true;
            if (requestCancel != null) requestCancel.Cancel();
            if (config.ChineseOnly) playbackWatch.Pause();
            current = null;
            previous.Clear();
            RestartCue();
            Save();
            UpdateStatus();
            if (enabled && !config.ChineseOnly) RequestRead();
        };
        chineseOnly.FontWeight = FontWeights.SemiBold;
        playback.Children.Add(chineseOnly);
        var follow = new CheckBox { Content = "跟随 Netflix 原字幕位置", IsChecked = config.FollowSubtitle };
        follow.Click += delegate { config.FollowSubtitle = follow.IsChecked == true; AppearanceChanged(); };
        AddRow(form, "字幕位置", follow);
        var context = new CheckBox { Content = "参考最近三条字幕", IsChecked = config.Context };
        context.Click += delegate
        {
            config.Context = context.IsChecked == true;
            RestartCue();
            Save();
        };
        AddRow(form, "对白上下文", context);
        var font = Choice(SubtitleOverlay.FontChoices, config.Font);
        font.SelectionChanged += delegate { config.Font = (string)font.SelectedItem; AppearanceChanged(); };
        AddRow(form, "字幕字体", font);
        var color = new Button { Content = config.Color };
        color.Click += delegate
        {
            using (var picker = new Forms.ColorDialog())
            {
                picker.Color = System.Drawing.ColorTranslator.FromHtml(config.Color);
                if (picker.ShowDialog() != Forms.DialogResult.OK) return;
                config.Color = String.Format("#{0:X2}{1:X2}{2:X2}", picker.Color.R, picker.Color.G, picker.Color.B);
                color.Content = config.Color;
                AppearanceChanged();
            }
        };
        AddRow(form, "字幕颜色", color);
        AddSlider(form, "字号", 18, 76, config.Size, value => config.Size = value);
        AddSlider(form, "手动位置 (%)", 5, 75, config.Bottom, value => config.Bottom = value);
        AddSlider(form, "描边", 0, 8, config.Stroke, value => config.Stroke = value);
        AddSlider(form, "背景不透明度", 0, 100, config.Opacity, value => config.Opacity = value);
        log.IsReadOnly = true;
        log.AcceptsReturn = true;
        log.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        log.Height = 94;
        body.Children.Add(new Expander { Header = "事件日志", Content = log, Margin = new Thickness(6, 2, 6, 0) });

        Closed += delegate { ExitApp(); };
        ticker = new DispatcherTimer { Interval = IdleInterval };
        playbackWatch = new PlaybackWatch(RequestRead);
        ticker.Tick += async delegate { await Tick(false); };
        ticker.Start();
        UpdateStatus();
        Loaded += async delegate { await RefreshModels(true); };
    }

    private static Brush Brush(string color)
    {
        return (Brush)new BrushConverter().ConvertFromString(color);
    }

    private static Border Card(UIElement content)
    {
        return new Border
        {
            Background = Brushes.White, BorderBrush = Brush("#DFE7F1"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12), Child = content
        };
    }

    private static Style ButtonStyle()
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#F4F7FC")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#D9E3F0")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 31d));
        var template = new ControlTemplate(typeof(Button));
        var shell = new FrameworkElementFactory(typeof(Border), "ButtonSurface");
        shell.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        shell.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        shell.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        shell.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        shell.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        shell.AppendChild(presenter);
        template.VisualTree = shell;
        foreach (var condition in new[] { UIElement.IsMouseOverProperty, Button.IsPressedProperty })
        {
            var trigger = new Trigger {Property = condition, Value = true};
            trigger.Setters.Add(new Setter(UIElement.OpacityProperty, condition == Button.IsPressedProperty ? .72 : .89, "ButtonSurface"));
            template.Triggers.Add(trigger);
        }
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private static ComboBox Choice(string[] items, string selected)
    {
        var control = new ComboBox { ItemsSource = items, SelectedItem = selected };
        if (control.SelectedIndex < 0) control.SelectedIndex = 0;
        return control;
    }

    private static void AddRow(Grid grid, string label, FrameworkElement input)
    {
        int row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var caption = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        input.Margin = new Thickness(0, 3, 0, 3);
        Grid.SetRow(caption, row);
        Grid.SetRow(input, row);
        Grid.SetColumn(input, 1);
        grid.Children.Add(caption);
        grid.Children.Add(input);
    }

    private void AddSlider(Grid form, string label, int min, int max, int initial, Action<int> update)
    {
        var row = new DockPanel();
        var value = new TextBlock { Text = initial.ToString(), Width = 36, TextAlignment = TextAlignment.Right };
        var slider = new Slider { Minimum = min, Maximum = max, Value = initial, TickFrequency = 1, IsSnapToTickEnabled = true };
        slider.ValueChanged += delegate
        {
            int number = (int)slider.Value;
            value.Text = number.ToString();
            update(number);
            AppearanceChanged();
        };
        DockPanel.SetDock(value, Dock.Right);
        row.Children.Add(value);
        row.Children.Add(slider);
        AddRow(form, label, row);
    }

    private void AppearanceChanged()
    {
        lastShown = null;
        Render();
        Save();
    }

    private void OpenPanel()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        quitting = true;
        ticker.Stop();
        playbackWatch.Dispose();
        desktopControl.Dispose();
        if (requestCancel != null) requestCancel.Cancel();
        if (warmupCancel != null) warmupCancel.Cancel();
        engine.Dispose();
        overlay.Close();
        Application.Current.Shutdown();
    }

    private async Task RefreshModels(bool startEngine = false)
    {
        try
        {
            string[] names = await AvailableModels(startEngine);
            loading = true;
            models.Items.Clear();
            foreach (string model in names) models.Items.Add(model);
            if (!names.Contains(config.Model)) models.Items.Add(config.Model);
            models.SelectedItem = config.Model;
            loading = false;
            WriteLog(names.Length == 0 ? "Ollama 在线，尚无本地模型。" : "检测到 " + names.Length + " 个本地模型。");
        }
        catch (Exception) { WriteLog("模型列表不可用，请确认本地 Ollama 已启动。"); }
    }

    private async Task<string[]> AvailableModels(bool startEngine)
    {
        try { return await engine.GetModels(); }
        catch (HttpRequestException) { if (!startEngine) throw; }
        catch (OperationCanceledException) { if (!startEngine) throw; }

        string script = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "start.ps1"));
        await Task.Run(() =>
        {
            using (var process = Process.Start(new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\" -EngineOnly")
                { UseShellExecute = false, CreateNoWindow = true }))
            {
                if (!process.WaitForExit(15000) || process.ExitCode != 0)
                    throw new InvalidOperationException("无法启动本地模型服务。");
            }
        });
        return await engine.GetModels();
    }

    private void SetEnabled(bool value)
    {
        enabled = value;
        desktopControl.Publish(value, config);
        ticker.Interval = value ? ActiveInterval : IdleInterval;
        if (!value && requestCancel != null) requestCancel.Cancel();
        if (!value) playbackWatch.Pause();
        if (value) WarmModel();
        else if (warmupCancel != null) warmupCancel.Cancel();
        InvalidateCue();
        previous.Clear();
        current = null;
        episode = null;
        playbackPosition = null;
        wasForeground = false;
        WriteLog(value ? "开始读取 Netflix 官方应用。" : "字幕翻译已停止。");
        UpdateStatus();
        Render();
        if (value && !config.ChineseOnly) RequestRead();
    }

    private void WarmModel()
    {
        if (warmupCancel != null) warmupCancel.Cancel();
        warmupCancel = new CancellationTokenSource();
        warmup = TryWarmModel(config.Model, warmupCancel);
    }

    private async Task TryWarmModel(string model, CancellationTokenSource cancel)
    {
        try { await engine.WarmUp(model, cancel.Token); }
        catch (HttpRequestException) {}
        catch (OperationCanceledException) {}
        finally
        {
            if (warmupCancel == cancel) warmupCancel = null;
            cancel.Dispose();
        }
    }

    private ComboBox LanguageChoice(string code, Action<string> setLanguage)
    {
        var choice = new ComboBox
        {
            ItemsSource = SubtitleLanguage.Options, DisplayMemberPath = "Label",
            SelectedValuePath = "Code", SelectedValue = code
        };
        choice.SelectionChanged += delegate
        {
            var selected = choice.SelectedValue as string;
            if (selected == null) return;
            setLanguage(selected);
            if (requestCancel != null) requestCancel.Cancel();
            previous.Clear();
            received = hits = successes = 0;
            latencies.Clear();
            lastVisibleLatency = 0;
            RestartCue();
            Save();
            UpdateStatus();
            if (enabled && !config.ChineseOnly) RequestRead();
        };
        return choice;
    }

    private void RestartCue()
    {
        InvalidateCue();
        Render();
    }

    private void InvalidateCue()
    {
        generation++;
        readyKey = null;
        source = "";
        chinese = "";
        error = "";
        lastError = "";
        nextTry = DateTime.MinValue;
    }

    private void RequestRead()
    {
        if (Dispatcher.HasShutdownStarted) return;
        if (Interlocked.Exchange(ref pendingRead, 1) != 0) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async delegate
        {
            Interlocked.Exchange(ref pendingRead, 0);
            if (reading) { readAfterCurrent = true; return; }
            await Tick(true);
        }));
    }

    private async Task Tick(bool immediate)
    {
        if (quitting || !enabled || reading) return;
        if (config.SameLanguage || config.ChineseOnly || desktopControl.Connected)
        {
            if (ticker.Interval != IdleInterval) ticker.Interval = IdleInterval;
            if (current != null)
            {
                playbackWatch.Pause();
                if (requestCancel != null) requestCancel.Cancel();
                current = null;
                episode = null;
                RestartCue();
            }
            UpdateStatus();
            return;
        }
        reading = true;
        if (immediate) eventReads++;
        int revision = generation;
        long readStarted = Stopwatch.GetTimestamp();
        try
        {
            var sample = await playbackWatch.ReadAsync();
            if (quitting || !enabled || revision != generation) return;
            if (current != null && (sample == null || sample.Window != current.Window || sample.Episode != episode))
            {
                InvalidateCue();
                previous.Clear();
                received = hits = successes = 0;
                latencies.Clear();
                playbackPosition = null;
            }
            current = sample;
            episode = sample == null ? null : sample.Episode;
            ObservePlaybackPosition(sample);
            var text = sample == null ? "" : sample.Text;
            if (text != source)
            {
                if (!String.IsNullOrEmpty(source))
                {
                    if (previous.Count == 3) previous.Dequeue();
                    previous.Enqueue(source);
                }
                source = text;
                generation++;
                readyKey = null;
                chinese = "";
                error = "";
                nextTry = DateTime.MinValue;
                if (source.Length > 0)
                {
                    if (requestCancel != null && config.Model == recentModel &&
                        DateTime.UtcNow - lastModelResponse < TimeSpan.FromMinutes(4))
                        requestCancel.CancelAfter(100);
                    received++;
                    cueArrived = DateTime.UtcNow;
                }
            }
            bool foreground = sample != null && sample.Window == NetflixReader.GetForegroundWindow();
            if (foreground && !wasForeground && source.Length > 0 && chinese.Length == 0)
                cueArrived = DateTime.UtcNow;
            wasForeground = foreground;
            var interval = foreground ? ActiveInterval : IdleInterval;
            if (ticker.Interval != interval) ticker.Interval = interval;
            if (foreground && source.Length > 0 && chinese.Length == 0)
            {
                string key = String.Join("\n", config.Model, config.SourceLanguageCode, config.TargetLanguageCode,
                    config.Context.ToString(), episode, source);
                string cached;
                if (cache.TryGetValue(key, out cached))
                {
                    if (key != readyKey || readStarted >= verifiedAfter)
                    {
                        if (key == readyKey)
                        {
                            if (latencies.Count == 100) latencies.Dequeue();
                            latencies.Enqueue(readyModelLatency);
                            successes++;
                        }
                        else hits++;
                        readyKey = null;
                        chinese = cached;
                        error = "";
                        lastError = "";
                        lastVisibleLatency = (DateTime.UtcNow - cueArrived).TotalMilliseconds;
                    }
                }
                else if (!translating && (warmup == null || warmup.IsCompleted) && DateTime.UtcNow >= nextTry)
                    TranslateCurrent(key);
            }
            Render();
            UpdateStatus();
            if (telemetry && DateTime.UtcNow >= nextTelemetry)
            {
                WriteLog(String.Format("测试采样：读取 {0}，新译 {1}，缓存 {2}，浮层提交 {3}，尺寸 {4}×{5}，模型均值 {6:F0} ms，实际绘制 {7} 次，本句 {8:F0} ms，上方对齐 {9} 次，即时读取 {10} 次，字体 {11}，界面事件 {12} 次",
                    received, successes, hits, overlays, sample == null ? 0 : sample.Bounds.Width,
                    sample == null ? 0 : sample.Bounds.Height, latencies.Count == 0 ? 0 : latencies.Average(),
                    overlay.Painted, lastVisibleLatency, overlay.AlignedAbove, eventReads, overlay.ActualFont, playbackWatch.Signals));
                nextTelemetry = DateTime.UtcNow.AddSeconds(5);
            }
        }
        catch (Exception error)
        {
            current = null;
            RestartCue();
            WriteLog("读取异常：" + error.GetType().Name);
        }
        finally
        {
            reading = false;
            if (readAfterCurrent)
            {
                readAfterCurrent = false;
                RequestRead();
            }
        }
    }

    private void ObservePlaybackPosition(NetflixReader.Sample sample)
    {
        if (sample == null || !sample.Position.HasValue) return;
        var now = DateTime.UtcNow;
        if (playbackPosition.HasValue && Math.Abs(sample.Position.Value - playbackPosition.Value) >
            (now - playbackSampleTime).TotalSeconds * 2 + 4)
        {
            InvalidateCue();
            previous.Clear();
        }
        playbackPosition = sample.Position;
        playbackSampleTime = now;
    }

    private async void TranslateCurrent(string key)
    {
        int revision = generation;
        var elapsed = Stopwatch.StartNew();
        translating = true;
        var cancel = new CancellationTokenSource();
        requestCancel = cancel;
        var context = config.Context ? previous.ToArray() : new string[0];
        string model = config.Model;
        try
        {
            string text = await engine.Translate(source, context, model, cancel.Token,
                config.SourceLanguageCode, config.TargetLanguageCode);
            recentModel = model;
            lastModelResponse = DateTime.UtcNow;
            if (!cache.ContainsKey(key))
            {
                if (cacheKeys.Count >= CacheCapacity) cache.Remove(cacheKeys.Dequeue());
                cacheKeys.Enqueue(key);
            }
            cache[key] = text;
            if (revision == generation)
            {
                readyKey = key;
                readyModelLatency = elapsed.Elapsed.TotalMilliseconds;
                verifiedAfter = Stopwatch.GetTimestamp();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception failure)
        {
            if (revision != generation) return;
            error = failure.Message;
            nextTry = DateTime.UtcNow.AddSeconds(4);
            if (error != lastError) WriteLog(error);
            lastError = error;
        }
        finally
        {
            requestCancel = null;
            cancel.Dispose();
            translating = false;
        }
        RequestRead();
    }

    private void Render()
    {
        Rect bounds = current == null ? Rect.Empty : current.Bounds;
        Rect captionBounds = current == null || !config.FollowSubtitle ? Rect.Empty : current.CaptionBounds;
        string display = enabled && !config.SameLanguage && !config.ChineseOnly && !desktopControl.Connected &&
            current != null && current.Window == NetflixReader.GetForegroundWindow()
            ? chinese : "";
        if (display == lastShown && bounds == lastBounds && captionBounds == lastCaptionBounds) return;
        if (display.Length > 0) overlays++;
        overlay.Present(bounds, captionBounds, current == null ? 0 : NetflixReader.GetDpiForWindow(current.Window), display, config);
        lastShown = display;
        lastBounds = bounds;
        lastCaptionBounds = captionBounds;
    }

    private void UpdateStatus()
    {
        toggle.Content = enabled ? "停止翻译" : "开始翻译";
        readout.Text = !enabled ? "已停止"
            : config.SameLanguage ? "源语言与目标语言相同 · 显示原字幕"
            : desktopControl.Connected ? (config.ChineseOnly ? "已连接 · 仅译文" : "已连接 · 双语")
            : config.ChineseOnly ? (!desktopControl.IsListening ? "字幕控制接口未启动"
                : "等待 Netflix 字幕扩展连接。请打开或刷新播放页。")
            : current == null ? "等待 Netflix 官方应用进入播放页"
            : error.Length > 0 ? error : current.Window != NetflixReader.GetForegroundWindow() ? "已连接 · 切回 Netflix 继续翻译"
            : source.Length == 0 ? "已连接 · 等待原字幕"
            : chinese.Length == 0 ? "已读到字幕 · 翻译中" : "同步显示中";
        string placement = !config.FollowSubtitle ? "使用手动字幕位置"
            : config.ChineseOnly ? "译文放在原字幕位置" : "译文显示在原字幕上方";
        string playerStatus = enabled ? desktopControl.PlayerStatus : "";
        performance.Text = desktopControl.Connected ? placement +
            (playerStatus.Length == 0 ? "" : " · " + playerStatus) + "\n" + desktopControl.Statistics
            : config.ChineseOnly ? placement
            : String.Format("已读 {0} 条 · 缓存命中 {1} 次 · 本句 {2:F0} ms\n缓存 {3}/{4} 条 · 模型平均 {5:F0} ms",
            received, hits, lastVisibleLatency, cache.Count, CacheCapacity, latencies.Count == 0 ? 0 : latencies.Average());
    }

    private void WriteLog(string text)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + "  " + text;
        if (events.Count >= 40) events.Dequeue();
        events.Enqueue(line);
        log.Text = String.Join(Environment.NewLine, events);
        log.ScrollToEnd();
        try
        {
            Directory.CreateDirectory(Data);
            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 128 * 1024) File.WriteAllText(LogFile, "");
            File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd ") + line + Environment.NewLine);
        }
        catch (IOException) {}
        catch (UnauthorizedAccessException) {}
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return;
            var saved = new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(SettingsFile));
            config.Model = saved.Model ?? config.Model;
            config.SourceLanguageCode = SubtitleLanguage.Find(saved.SourceLanguageCode, SubtitleLanguage.DefaultSource).Code;
            config.TargetLanguageCode = SubtitleLanguage.Find(saved.TargetLanguageCode, SubtitleLanguage.DefaultTarget).Code;
            if (SubtitleOverlay.FontChoices.Contains(saved.Font) &&
                !(saved.TypographyVersion == 0 && saved.Font == "Microsoft YaHei UI"))
                config.Font = saved.Font;
            if (System.Text.RegularExpressions.Regex.IsMatch(saved.Color ?? "", "^#[A-Fa-f0-9]{6}$")) config.Color = saved.Color;
            config.Size = Math.Max(18, Math.Min(76, saved.Size));
            config.Bottom = Math.Max(5, Math.Min(75, saved.Bottom));
            config.Stroke = Math.Max(0, Math.Min(8, saved.Stroke));
            config.Opacity = Math.Max(0, Math.Min(100, saved.Opacity));
            config.ChineseOnly = saved.ChineseOnly;
            config.Context = saved.Context;
            config.FollowSubtitle = saved.FollowSubtitle;
        }
        catch (Exception) {}
    }

    private void Save()
    {
        desktopControl.Publish(enabled, config);
        try
        {
            Directory.CreateDirectory(Data);
            config.TypographyVersion = 1;
            string temp = SettingsFile + ".tmp";
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(config));
            if (File.Exists(SettingsFile)) File.Replace(temp, SettingsFile, null);
            else File.Move(temp, SettingsFile);
        }
        catch (IOException) { WriteLog("无法保存设置。"); }
        catch (UnauthorizedAccessException) { WriteLog("无法保存设置。"); }
    }
}
