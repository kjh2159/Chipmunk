using System.Drawing;
using Forms = System.Windows.Forms;

namespace Chipmunk.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Icon _applicationIcon;
    private readonly Forms.ToolStripMenuItem _showItem;
    private readonly Forms.ToolStripMenuItem _clickThroughItem;
    private readonly Action _showOrHide;
    private readonly Action _showSettings;
    private readonly Action _showDetails;
    private readonly Func<Task> _rescan;
    private readonly Func<bool, Task> _setClickThrough;
    private readonly Func<Task> _resetPosition;
    private readonly Func<Task> _exit;

    public TrayIconService(
        Action showOrHide,
        Action showSettings,
        Action showDetails,
        Func<Task> rescan,
        Func<bool, Task> setClickThrough,
        Func<Task> resetPosition,
        Func<Task> exit)
    {
        _showOrHide = showOrHide;
        _showSettings = showSettings;
        _showDetails = showDetails;
        _rescan = rescan;
        _setClickThrough = setClickThrough;
        _resetPosition = resetPosition;
        _exit = exit;

        _showItem = new Forms.ToolStripMenuItem("위젯 숨기기");
        _showItem.Click += (_, _) => _showOrHide();

        _clickThroughItem = new Forms.ToolStripMenuItem("클릭 통과 모드")
        {
            CheckOnClick = true
        };
        _clickThroughItem.Click += async (_, _) =>
            await _setClickThrough(_clickThroughItem.Checked);

        var settingsItem = new Forms.ToolStripMenuItem("설정");
        settingsItem.Click += (_, _) => _showSettings();
        var detailsItem = new Forms.ToolStripMenuItem("상세 모니터");
        detailsItem.Click += (_, _) => _showDetails();
        var rescanItem = new Forms.ToolStripMenuItem("센서 새로 검색");
        rescanItem.Click += async (_, _) => await _rescan();
        var resetPositionItem = new Forms.ToolStripMenuItem("기본 위치로 복원");
        resetPositionItem.Click += async (_, _) => await _resetPosition();
        var exitItem = new Forms.ToolStripMenuItem("프로그램 종료");
        exitItem.Click += async (_, _) => await _exit();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.AddRange(
        [
            _showItem,
            detailsItem,
            settingsItem,
            new Forms.ToolStripSeparator(),
            rescanItem,
            _clickThroughItem,
            resetPositionItem,
            new Forms.ToolStripSeparator(),
            exitItem
        ]);

        _applicationIcon = LoadApplicationIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _applicationIcon,
            Text = "Chipmunk",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => _showOrHide();
    }

    public void Synchronize(bool widgetVisible, bool clickThrough)
    {
        _showItem.Text = widgetVisible ? "위젯 숨기기" : "위젯 표시";
        _clickThroughItem.Checked = clickThrough;
    }

    public void SetToolTip(string value)
    {
        // NotifyIcon limits tooltip text to 63 characters on supported Windows versions.
        _notifyIcon.Text = string.IsNullOrWhiteSpace(value)
            ? "Chipmunk"
            : value[..Math.Min(63, value.Length)];
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _applicationIcon.Dispose();
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
            {
                var icon = Icon.ExtractAssociatedIcon(executablePath);
                if (icon is not null)
                {
                    return icon;
                }
            }
        }
        catch
        {
            // The embedded executable icon is preferred, but a missing icon must
            // never prevent the monitoring widget from starting.
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
