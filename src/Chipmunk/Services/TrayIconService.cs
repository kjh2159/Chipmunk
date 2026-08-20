using System.Drawing;
using Forms = System.Windows.Forms;

namespace Chipmunk.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Icon _applicationIcon;
    private readonly ILocalizationService _localization;
    private readonly Forms.ToolStripMenuItem _showItem;
    private readonly Forms.ToolStripMenuItem _clickThroughItem;
    private readonly Forms.ToolStripMenuItem _settingsItem;
    private readonly Forms.ToolStripMenuItem _detailsItem;
    private readonly Forms.ToolStripMenuItem _rescanItem;
    private readonly Forms.ToolStripMenuItem _resetPositionItem;
    private readonly Forms.ToolStripMenuItem _exitItem;
    private readonly Action _showOrHide;
    private readonly Action _showSettings;
    private readonly Action _showDetails;
    private readonly Func<Task> _rescan;
    private readonly Func<bool, Task> _setClickThrough;
    private readonly Func<Task> _resetPosition;
    private readonly Func<Task> _exit;
    private bool _widgetVisible = true;

    public TrayIconService(
        ILocalizationService localization,
        Action showOrHide,
        Action showSettings,
        Action showDetails,
        Func<Task> rescan,
        Func<bool, Task> setClickThrough,
        Func<Task> resetPosition,
        Func<Task> exit)
    {
        _localization = localization;
        _showOrHide = showOrHide;
        _showSettings = showSettings;
        _showDetails = showDetails;
        _rescan = rescan;
        _setClickThrough = setClickThrough;
        _resetPosition = resetPosition;
        _exit = exit;

        _showItem = new Forms.ToolStripMenuItem();
        _showItem.Click += (_, _) => _showOrHide();

        _clickThroughItem = new Forms.ToolStripMenuItem()
        {
            CheckOnClick = true
        };
        _clickThroughItem.Click += async (_, _) =>
            await _setClickThrough(_clickThroughItem.Checked);

        _settingsItem = new Forms.ToolStripMenuItem();
        _settingsItem.Click += (_, _) => _showSettings();
        _detailsItem = new Forms.ToolStripMenuItem();
        _detailsItem.Click += (_, _) => _showDetails();
        _rescanItem = new Forms.ToolStripMenuItem();
        _rescanItem.Click += async (_, _) => await _rescan();
        _resetPositionItem = new Forms.ToolStripMenuItem();
        _resetPositionItem.Click += async (_, _) => await _resetPosition();
        _exitItem = new Forms.ToolStripMenuItem();
        _exitItem.Click += async (_, _) => await _exit();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.AddRange(
        [
            _showItem,
            _detailsItem,
            _settingsItem,
            new Forms.ToolStripSeparator(),
            _rescanItem,
            _clickThroughItem,
            _resetPositionItem,
            new Forms.ToolStripSeparator(),
            _exitItem
        ]);

        RefreshTexts();
        _localization.LanguageChanged += OnLanguageChanged;

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
        _widgetVisible = widgetVisible;
        _showItem.Text = _localization.Get(
            widgetVisible ? "TrayHideWidget" : "TrayShowWidget");
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
        _localization.LanguageChanged -= OnLanguageChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _applicationIcon.Dispose();
    }

    private void OnLanguageChanged(Chipmunk.Models.AppLanguage language) => RefreshTexts();

    private void RefreshTexts()
    {
        _showItem.Text = _localization.Get(
            _widgetVisible ? "TrayHideWidget" : "TrayShowWidget");
        _clickThroughItem.Text = _localization.Get("ClickThroughMode");
        _settingsItem.Text = _localization.Get("TraySettings");
        _detailsItem.Text = _localization.Get("TrayDetailedMonitor");
        _rescanItem.Text = _localization.Get("TrayRescanSensors");
        _resetPositionItem.Text = _localization.Get("TrayResetPosition");
        _exitItem.Text = _localization.Get("TrayExit");
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
