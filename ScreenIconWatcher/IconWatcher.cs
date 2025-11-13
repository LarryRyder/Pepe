using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ScreenIconWatcher;

internal sealed class IconWatcher : IDisposable
{
    private readonly Mat _template;
    private readonly double _threshold;
    private readonly TimeSpan _pollInterval;

    private Thread? _workerThread;
    private CancellationTokenSource? _cts;

    public IconWatcher(string templateImagePath, double threshold, TimeSpan pollInterval)
    {
        if (!File.Exists(templateImagePath))
        {
            throw new FileNotFoundException($"Template image not found: {templateImagePath}");
        }

        _template = Cv2.ImRead(templateImagePath, ImreadModes.Color);
        if (_template.Empty())
        {
            throw new InvalidOperationException("Could not read template image or image is empty.");
        }

        _threshold = Math.Clamp(threshold, 0.0, 1.0);
        _pollInterval = pollInterval < TimeSpan.FromMilliseconds(50)
            ? TimeSpan.FromMilliseconds(50)
            : pollInterval;
    }

    public event EventHandler<IconMatchEventArgs>? IconMatched;

    public event EventHandler<Exception>? WatcherError;

    public bool IsRunning => _workerThread != null;

    public void Start()
    {
        if (_workerThread != null)
        {
            throw new InvalidOperationException("Watcher is already running.");
        }

        _cts = new CancellationTokenSource();
        _workerThread = new Thread(() => WatchLoop(_cts.Token))
        {
            IsBackground = true,
            Name = nameof(IconWatcher)
        };
        _workerThread.Start();
    }

    public void Stop()
    {
        if (_cts == null)
        {
            return;
        }

        try
        {
            _cts.Cancel();
            if (_workerThread != null && !_workerThread.Join(TimeSpan.FromSeconds(2)))
            {
                _workerThread.Interrupt();
            }
        }
        finally
        {
            _workerThread = null;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void WatchLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                using var screenshot = CaptureScreen();
                using var screenshotMat = BitmapConverter.ToMat(screenshot);
                using var result = new Mat();

                Cv2.MatchTemplate(screenshotMat, _template, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

                if (maxVal >= _threshold)
                {
                    IconMatched?.Invoke(
                        this,
                        new IconMatchEventArgs(
                            new Point(maxLoc.X, maxLoc.Y),
                            maxVal,
                            DateTimeOffset.Now));
                }

                if (token.WaitHandle.WaitOne(_pollInterval))
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            WatcherError?.Invoke(this, ex);
        }
    }

    private static Bitmap CaptureScreen()
    {
        var bounds = Screen.PrimaryScreen?.Bounds
                     ?? throw new InvalidOperationException("Could not determine primary screen bounds.");

        var screenshot = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(screenshot);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        return screenshot;
    }

    public void Dispose()
    {
        Stop();
        _template.Dispose();
    }
}

internal sealed class IconMatchEventArgs : EventArgs
{
    public IconMatchEventArgs(Point location, double score, DateTimeOffset timestamp)
    {
        Location = location;
        Score = score;
        Timestamp = timestamp;
    }

    public Point Location { get; }

    public double Score { get; }

    public DateTimeOffset Timestamp { get; }
}
