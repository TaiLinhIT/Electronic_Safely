using System.Drawing;

namespace GPT9000_WinForms_TestSystem;

/// <summary>
/// Đăng nhập tài khoản quản lý - tái hiện Log_In của Hipot_GPT9801:
/// mật khẩu lưu trong Config\pw.org, sai quá 3 lần thì đóng form,
/// có thể đổi mật khẩu bằng cách tick "Đổi mật khẩu".
/// </summary>
public sealed class LogInForm : Form
{
    private readonly PasswordStore _store;
    private readonly TextBox _tbPassword = new();
    private readonly TextBox _tbNew1 = new();
    private readonly TextBox _tbNew2 = new();
    private readonly CheckBox _chkChange = new();
    private readonly Label _hint = new();
    private int _tries;

    public bool Admin { get; private set; }

    public LogInForm(PasswordStore store)
    {
        AppBranding.ApplyIcon(this);
        _store = store;

        Text = "Đăng Nhập Tài Khoản Quản Lý";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 300);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(248, 250, 252);

        var title = new Label
        {
            Text = "ĐĂNG NHẬP", Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 95), AutoSize = true, Location = new Point(150, 12)
        };

        var lbPass = new Label { Text = "Mật khẩu:", Location = new Point(20, 70), AutoSize = true };
        _tbPassword.Location = new Point(150, 67); _tbPassword.Size = new Size(280, 25);
        _tbPassword.UseSystemPasswordChar = true;

        _chkChange.Text = "Đổi mật khẩu"; _chkChange.Location = new Point(20, 108); _chkChange.AutoSize = true;
        _chkChange.CheckedChanged += (_, _) => UpdateState();

        var lbNew1 = new Label { Text = "Mật khẩu mới:", Location = new Point(20, 145), AutoSize = true };
        _tbNew1.Location = new Point(150, 142); _tbNew1.Size = new Size(280, 25);
        _tbNew1.UseSystemPasswordChar = true;

        var lbNew2 = new Label { Text = "Nhập lại mới:", Location = new Point(20, 180), AutoSize = true };
        _tbNew2.Location = new Point(150, 177); _tbNew2.Size = new Size(280, 25);
        _tbNew2.UseSystemPasswordChar = true;

        _hint.Text = "Nhập mật khẩu cũ và mới, rồi bấm Đồng ý để đổi mật khẩu.";
        _hint.ForeColor = Color.FromArgb(0, 128, 128); _hint.Location = new Point(20, 206);
        _hint.Font = new Font("Segoe UI", 9F);
        _hint.Size = new Size(420, 34); _hint.Visible = false;

        var ok = new Button { Text = "Đồng ý", Location = new Point(215, 245), Size = new Size(100, 35), BackColor = Color.LightGreen };
        ok.Click += (_, _) => OnOk();
        var cancel = new Button { Text = "Thoát", Location = new Point(330, 245), Size = new Size(100, 35) };
        cancel.Click += (_, _) => { Admin = false; Close(); };
        AcceptButton = ok; CancelButton = cancel;

        Controls.AddRange(new Control[] { title, lbPass, _tbPassword, _chkChange, lbNew1, _tbNew1, lbNew2, _tbNew2, _hint, ok, cancel });
        UpdateState();
    }

    private void UpdateState()
    {
        bool on = _chkChange.Checked;
        _tbNew1.Enabled = on; _tbNew2.Enabled = on; _hint.Visible = on;
        _tbNew1.Clear(); _tbNew2.Clear();
    }

    private void OnOk()
    {
        string current = _store.Read();

        if (!_chkChange.Checked)
        {
            if (_tbPassword.Text == current)
            {
                Admin = true;
                Close();
                return;
            }
            _tbPassword.Clear();
            _tries++;
            if (_tries < 4)
            {
                VietnameseMessageBox.Show("Nhập sai mật khẩu người dùng quản lý", "Thông báo lỗi",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            VietnameseMessageBox.Show("Bạn đã nhập sai mật khẩu quá 3 lần!", "Thông báo lỗi",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
            Admin = false;
            Close();
            return;
        }

        if (_tbPassword.Text != current)
        {
            VietnameseMessageBox.Show("Nhập sai mật khẩu người dùng quản lý", "Thông báo lỗi",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_tbNew1.Text != _tbNew2.Text || _tbNew1.Text.Length == 0)
        {
            VietnameseMessageBox.Show("Mật khẩu mới không trùng nhau!", "Thông báo lỗi",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _store.Write(_tbNew1.Text);
        Admin = true;
        Close();
    }
}
