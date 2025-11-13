using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ScreenIconWatcher;

internal sealed class MainForm : Form
{
    private readonly TextBox _templatePathTextBox = new();
    private readonly Button _browseButton = new();
    private readonly NumericUpDown _thresholdUpDown = new();
    private readonly NumericUpDown _pollIntervalUpDown = new();
    private readonly TextBox _actionCommandTextBox = new();
    private readonly Button _startStopButton = new();
    private readonly Label _statusLabel = new();
    private readonly ListBox _logListBox = new();

    private IconWatcher? _watcher;
    private string _currentActionCommand = string.Empty;
    private DateTimeOffset _lastActionTime = DateTimeOffset.MinValue;
    private readonly TimeSpan _actionCooldown = TimeSpan.FromSeconds(5);

    public MainForm()
    {
        InitializeComponent();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopWatching();
        base.OnFormClosing(e);
    }

    private void InitializeComponent()
    {
        Text = "Screen Icon Watcher";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(520, 420);
        Size = new Size(640, 480);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 7,
            Padding = new Padding(12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var templateLabel = new Label
        {
            Text = "Vorlagenbild:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        };
        _templatePathTextBox.PlaceholderText = "Pfad zum Icon-Screenshot (.png, .jpg, ...)";
        _templatePathTextBox.Dock = DockStyle.Fill;
        _browseButton.Text = "Durchsuchen...";
        _browseButton.AutoSize = true;
        _browseButton.Click += OnBrowseClicked;

        layout.Controls.Add(templateLabel, 0, 0);
        layout.Controls.Add(_templatePathTextBox, 1, 0);
        layout.Controls.Add(_browseButton, 2, 0);

        var thresholdLabel = new Label
        {
            Text = "Schwellwert:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        };
        _thresholdUpDown.DecimalPlaces = 2;
        _thresholdUpDown.Minimum = 0;
        _thresholdUpDown.Maximum = 1;
        _thresholdUpDown.Increment = 0.01M;
        _thresholdUpDown.Value = 0.90M;
        _thresholdUpDown.Width = 80;
        _thresholdUpDown.Dock = DockStyle.Left;
        var thresholdInfoLabel = new Label
        {
            Text = "0.50 - 0.99",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Color.DimGray,
        };

        layout.Controls.Add(thresholdLabel, 0, 1);
        layout.Controls.Add(_thresholdUpDown, 1, 1);
        layout.Controls.Add(thresholdInfoLabel, 2, 1);

        var pollLabel = new Label
        {
            Text = "Abtastrate (ms):",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        };
        _pollIntervalUpDown.Minimum = 50;
        _pollIntervalUpDown.Maximum = 10000;
        _pollIntervalUpDown.Increment = 50;
        _pollIntervalUpDown.Value = 500;
        _pollIntervalUpDown.Width = 100;
        _pollIntervalUpDown.Dock = DockStyle.Left;
        var pollInfoLabel = new Label
        {
            Text = "50 - 10000",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Color.DimGray,
        };

        layout.Controls.Add(pollLabel, 0, 2);
        layout.Controls.Add(_pollIntervalUpDown, 1, 2);
        layout.Controls.Add(pollInfoLabel, 2, 2);

        var actionLabel = new Label
        {
            Text = "Aktion (optional):",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        };
        _actionCommandTextBox.PlaceholderText = "Befehl oder Pfad, der beim Treffer gestartet wird (z.B. notepad)";
        _actionCommandTextBox.Dock = DockStyle.Fill;
        layout.Controls.Add(actionLabel, 0, 3);
        layout.Controls.Add(_actionCommandTextBox, 1, 3);
        layout.SetColumnSpan(_actionCommandTextBox, 2);

        var actionHintLabel = new Label
        {
            Text = "Tipp: Parameter mit Anführungszeichen gruppieren (z.B. \"C:\\Tools\\app.exe\" --flag)",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Anchor = AnchorStyles.Left,
        };
        layout.Controls.Add(actionHintLabel, 1, 4);
        layout.SetColumnSpan(actionHintLabel, 2);

        _startStopButton.Text = "Starten";
        _startStopButton.AutoSize = true;
        _startStopButton.Anchor = AnchorStyles.Right;
        _startStopButton.Click += OnStartStopClicked;
        layout.Controls.Add(_startStopButton, 2, 5);

        _logListBox.Dock = DockStyle.Fill;
        _logListBox.HorizontalScrollbar = true;
        layout.Controls.Add(_logListBox, 0, 5);
        layout.SetColumnSpan(_logListBox, 2);

        _statusLabel.Text = "Bereit.";
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.DimGray;
        _statusLabel.Dock = DockStyle.Fill;
        layout.Controls.Add(_statusLabel, 0, 6);
        layout.SetColumnSpan(_statusLabel, 3);

        Controls.Add(layout);
    }

    private void OnBrowseClicked(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Bilddateien (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Alle Dateien (*.*)|*.*",
            Title = "Vorlagenbild auswählen",
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _templatePathTextBox.Text = dialog.FileName;
        }
    }

    private void OnStartStopClicked(object? sender, EventArgs e)
    {
        if (_watcher?.IsRunning == true)
        {
            StopWatching();
        }
        else
        {
            StartWatching();
        }
    }

    private void StartWatching()
    {
        string templatePath = _templatePathTextBox.Text.Trim();
        if (!File.Exists(templatePath))
        {
            MessageBox.Show(this, "Bitte ein gültiges Vorlagenbild auswählen.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        double threshold = (double)_thresholdUpDown.Value;
        int pollMs = (int)_pollIntervalUpDown.Value;
        _currentActionCommand = _actionCommandTextBox.Text.Trim();

        try
        {
            _watcher = new IconWatcher(templatePath, threshold, TimeSpan.FromMilliseconds(pollMs));
            _watcher.IconMatched += OnIconMatched;
            _watcher.WatcherError += OnWatcherError;
            _watcher.Start();

            _startStopButton.Text = "Stoppen";
            _statusLabel.Text = "Überwachung aktiv...";
            _statusLabel.ForeColor = Color.ForestGreen;
            AppendLog($"Suche nach Icon '{Path.GetFileName(templatePath)}' (Score ≥ {threshold:F2}, {pollMs} ms).");
        }
        catch (Exception ex)
        {
            _currentActionCommand = string.Empty;
            _watcher?.Dispose();
            _watcher = null;
            MessageBox.Show(this, ex.Message, "Fehler beim Start", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopWatching()
    {
        if (_watcher == null)
        {
            return;
        }

        _watcher.IconMatched -= OnIconMatched;
        _watcher.WatcherError -= OnWatcherError;
        _watcher.Dispose();
        _watcher = null;
        _currentActionCommand = string.Empty;
        _lastActionTime = DateTimeOffset.MinValue;

        _startStopButton.Text = "Starten";
        _statusLabel.Text = "Bereit.";
        _statusLabel.ForeColor = Color.DimGray;
        AppendLog("Überwachung gestoppt.");
    }

    private void OnIconMatched(object? sender, IconMatchEventArgs e)
    {
        if (!IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            AppendLog($"Icon erkannt (Score {e.Score:F3}) bei Position {e.Location}.");
            TryExecuteAction(e);
        }));
    }

    private void OnWatcherError(object? sender, Exception e)
    {
        if (!IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            AppendLog($"Überwachung gestoppt: {e.Message}");
            MessageBox.Show(this, e.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            StopWatching();
        }));
    }

    private void TryExecuteAction(IconMatchEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentActionCommand))
        {
            return;
        }

        if (e.Timestamp - _lastActionTime < _actionCooldown)
        {
            AppendLog("Aktion übersprungen (Cooldown aktiv).");
            return;
        }

        var (fileName, arguments) = ParseCommand(_currentActionCommand);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            AppendLog("Aktion konnte nicht gestartet werden: ungültiger Befehl.");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
            };
            Process.Start(startInfo);
            _lastActionTime = e.Timestamp;
            AppendLog($"Aktion ausgeführt: {fileName} {arguments}".Trim());
        }
        catch (Exception ex)
        {
            AppendLog($"Aktion fehlgeschlagen: {ex.Message}");
        }
    }

    private static (string FileName, string Arguments) ParseCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return (string.Empty, string.Empty);
        }

        var parts = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in command.Trim())
        {
            switch (c)
            {
                case ' ' when !inQuotes:
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                    break;
                case '"':
                    inQuotes = !inQuotes;
                    break;
                default:
                    current.Append(c);
                    break;
            }
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        if (parts.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        string fileName = parts[0];
        string arguments = parts.Count > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;
        return (fileName, arguments);
    }

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logListBox.Items.Add($"{timestamp}  {message}");
        _logListBox.TopIndex = Math.Max(_logListBox.Items.Count - 1, 0);
    }
}
