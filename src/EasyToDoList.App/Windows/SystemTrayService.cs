using System.Drawing;
using System.Windows.Forms;

namespace EasyToDoList.App.Windows;

public sealed class SystemTrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu = new();

    public SystemTrayService(Action overlay, Action dashboard, Action exit)
    {
        _menu.Items.Add("Overlay", null, (_, _) => overlay());
        _menu.Items.Add("Dashboard", null, (_, _) => dashboard());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => exit());
        _icon = new NotifyIcon
        {
            Text = "EasyToDoList",
            Icon = SystemIcons.Application,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => dashboard();
        _icon.BalloonTipClicked += (_, _) => dashboard();
    }

    public void Notify(string title, string body) =>
        _icon.ShowBalloonTip(5000, title, body, ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
