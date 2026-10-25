namespace GPT9000_WinForms_TestSystem;

/// <summary>Hộp thông báo có nút tiếng Việt kể cả khi Windows dùng tiếng Anh.</summary>
internal static class VietnameseMessageBox
{
    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        => Show(Form.ActiveForm, text, caption, buttons, icon);

    public static DialogResult Show(IWin32Window? owner, string text, string caption,
        MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        using var dialog = CreateDialog(text, caption, buttons, icon);
        if (owner is null) dialog.StartPosition = FormStartPosition.CenterScreen;
        return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }

    private static Form CreateDialog(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        var dialog = new Form
        {
            Text = caption, FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, ShowIcon = false,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi,
            Font = new Font("Segoe UI", 10F), Padding = new Padding(16)
        };
        var layout = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2, RowCount = 2, Dock = DockStyle.Fill
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var iconImage = (icon switch
        {
            MessageBoxIcon.Error => SystemIcons.Error,
            MessageBoxIcon.Warning => SystemIcons.Warning,
            MessageBoxIcon.Question => SystemIcons.Question,
            _ => SystemIcons.Information
        }).ToBitmap();
        dialog.Disposed += (_, _) => iconImage.Dispose();
        layout.Controls.Add(new PictureBox { Image = iconImage, Size = new Size(40, 40),
            SizeMode = PictureBoxSizeMode.CenterImage, Margin = new Padding(0, 0, 12, 16) }, 0, 0);
        layout.Controls.Add(new Label { Text = text, UseMnemonic = false, AutoSize = true, MinimumSize = new Size(320, 40),
            MaximumSize = new Size(620, 0), Margin = new Padding(0, 0, 0, 16) }, 1, 0);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill, Margin = Padding.Empty
        };
        Button AddButton(string label, DialogResult result)
        {
            var button = new Button { Text = label, DialogResult = result,
                AutoSize = true, MinimumSize = new Size(100, 36), Margin = new Padding(8, 0, 0, 0) };
            actions.Controls.Add(button);
            return button;
        }
        if (buttons == MessageBoxButtons.YesNo)
        {
            dialog.CancelButton = AddButton("Không", DialogResult.No);
            dialog.AcceptButton = AddButton("Có", DialogResult.Yes);
        }
        else
        {
            var accept = AddButton("Đồng ý", DialogResult.OK);
            dialog.AcceptButton = accept;
            dialog.CancelButton = accept;
        }
        layout.Controls.Add(actions, 0, 1);
        layout.SetColumnSpan(actions, 2);
        dialog.Controls.Add(layout);
        dialog.ActiveControl = (Control)dialog.AcceptButton!;
        return dialog;
    }
}
