namespace GPT9000_WinForms_TestSystem;

public partial class MainForm
{
    private bool _connecting;
    private bool _shuttingDown;

    private void SetConnectionBusy(bool busy)
    {
        _btnReconnect.Enabled = !busy;
        _btnReconnect.Text = busy ? "Đang kết nối..." : "Kết nối lại";
        _btnSelectProgram.Enabled = !busy;
        _btnUnlockSettings.Enabled = !busy;
        _status.Enabled = !busy;
        DisableSettings(busy || !_settingsAccess.Unlocked);
    }
}
