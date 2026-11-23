using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;

internal static class NetflixReader
{
    internal sealed class Sample
    {
        public IntPtr Window;
        public string Episode;
        public string Text;
        public double? Position;
        public Rect Bounds;
        public Rect CaptionBounds = Rect.Empty;
        internal AutomationElement Document;
    }

    private delegate bool WindowCallback(IntPtr hwnd, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct ClientRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct ScreenPoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int length);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out ClientRect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref ScreenPoint point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    internal static volatile bool DebugTree = false;
    internal static Action<string> Trace = null;
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    private static readonly TreeWalker Walker = TreeWalker.ControlViewWalker;

    public static Sample Read() { return Read(IntPtr.Zero, null); }

    internal static Sample Read(IntPtr watchedWindow, AutomationElement watchedDocument)
    {
        if (watchedDocument != null && watchedWindow == GetForegroundWindow())
        {
            try
            {
                var watched = ReadDocument(watchedWindow, watchedDocument);
                if (watched != null) return watched;
            }
            catch (ElementNotAvailableException) {}
            catch (COMException) {}
            catch (InvalidOperationException) {}
        }
        Sample result = null;
        EnumWindows(delegate(IntPtr hwnd, IntPtr data)
        {
            if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
            var title = new StringBuilder(256);
            GetWindowText(hwnd, title, title.Capacity);
            bool hasTitle = String.Equals(title.ToString(), "Netflix", StringComparison.OrdinalIgnoreCase);
            if (!hasTitle && hwnd != GetForegroundWindow()) return true;
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            try
            {
                using (var process = Process.GetProcessById((int)pid))
                    if (process.ProcessName != "msedge" && process.ProcessName != "Netflix") return true;
                if (DebugTree && Trace != null) Trace("Netflix window title match " + hasTitle);
                result = ReadWindow(hwnd, hasTitle);
            }
            catch (ElementNotAvailableException) {}
            catch (COMException) {}
            catch (ArgumentException) {}
            catch (System.ComponentModel.Win32Exception) {}
            return result == null;
        }, IntPtr.Zero);
        return result;
    }

    private static IEnumerable<AutomationElement> Children(AutomationElement parent)
    {
        for (var node = Walker.GetFirstChild(parent); node != null; node = Walker.GetNextSibling(node))
            yield return node;
    }

    private static string Value(AutomationElement element)
    {
        object pattern;
        return element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)
            ? ((ValuePattern)pattern).Current.Value : "";
    }

    private static Sample ReadWindow(IntPtr hwnd, bool hasTitle)
    {
        var root = AutomationElement.FromHandle(hwnd);
        if (root == null) return null;
        var doc = root.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document),
            new PropertyCondition(AutomationElement.AutomationIdProperty, "RootWebArea")));
        if (doc == null) return null;
        if (!hasTitle && !String.Equals(doc.Current.Name, "Netflix", StringComparison.OrdinalIgnoreCase)) return null;
        return ReadDocument(hwnd, doc);
    }

    private static Sample ReadDocument(IntPtr hwnd, AutomationElement doc)
    {
        Uri url;
        if (!Uri.TryCreate(Value(doc), UriKind.Absolute, out url) || url.Scheme != "https" ||
            !String.Equals(url.Host, "www.netflix.com", StringComparison.OrdinalIgnoreCase))
            return null;
        var path = url.AbsolutePath.Split('/');
        if (path.Length < 3 || path[1] != "watch" || path[2].Length == 0 || !path[2].All(Char.IsDigit))
            return null;

        var viewport = doc.Current.BoundingRectangle;
        if (DebugTree && Trace != null) Trace("Document bounds " + viewport);
        if (viewport.IsEmpty || viewport.Width < 100 || viewport.Height < 100)
        {
            ClientRect client;
            var origin = new ScreenPoint();
            if (GetClientRect(hwnd, out client) && ClientToScreen(hwnd, ref origin) &&
                client.Right > 100 && client.Bottom > 100)
                viewport = new Rect(origin.X, origin.Y, client.Right, client.Bottom);
        }
        var mount = doc.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "appMountPoint"));
        if (viewport.IsEmpty || viewport.Width < 100 || viewport.Height < 100) return null;
        // Edge exposes only the fullscreen subtree under the document in fullscreen.
        var groups = Children(mount ?? doc).Where(node => node.Current.ControlType == ControlType.Group).ToList();
        var result = new Sample {Window = hwnd, Episode = path[2], Bounds = viewport, Text = "", Document = doc};
        foreach (var group in groups)
        {
            var direct = Children(group).ToList();
            var slider = direct.FirstOrDefault(node => node.Current.ControlType == ControlType.Slider);
            object range;
            if (slider != null && slider.TryGetCurrentPattern(RangeValuePattern.Pattern, out range))
            {
                var sliderValue = ((RangeValuePattern)range).Current;
                if (sliderValue.Maximum > 100) result.Position = sliderValue.Value;
            }
            if (DebugTree && Trace != null)
                Trace("player children: " + String.Join(" | ", direct.Select(n =>
                    n.Current.ControlType.ProgrammaticName + ":" + n.Current.AutomationId +
                    ":" + n.Current.ClassName + ":" + Math.Round(n.Current.BoundingRectangle.Top))));
            var lines = new List<string>();
            var captionBounds = Rect.Empty;
            foreach (var node in direct)
            {
                var state = node.Current;
                if (!IsSubtitle(state, viewport)) continue;
                var text = (state.Name ?? "").Trim();
                if (text.Length == 0 || text.Length > 250) continue;
                lines.Add(text);
                captionBounds.Union(state.BoundingRectangle);
            }
            if (lines.Count > 0 && lines.Count <= 6)
            {
                result.Text = String.Join("\n", lines);
                result.CaptionBounds = captionBounds;
                break;
            }
        }
        return result;
    }

    private static bool IsSubtitle(AutomationElement.AutomationElementInformation state, Rect viewport)
    {
        if (state.ControlType != ControlType.Text || state.IsOffscreen || !String.IsNullOrEmpty(state.AutomationId))
            return false;
        var bounds = state.BoundingRectangle;
        return !bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0 &&
            bounds.Top > viewport.Top + viewport.Height * 0.1 &&
            bounds.Bottom <= viewport.Bottom + 4 &&
            bounds.Left >= viewport.Left - 4 && bounds.Right <= viewport.Right + 4;
    }
}
