using System.Drawing;

namespace GPT9000_WinForms_TestSystem;

/// <summary>
/// Lựa chọn chương trình phép thử - tái hiện OpenProgram của Hipot_GPT9801:
/// liệt kê các file <c>program\*.csv</c> và trả về file được chọn.
/// </summary>
public sealed class OpenProgramForm : Form
{
    private readonly DataGridView _grid = new();

    /// <summary>File csv được chọn (vd. "program.csv"), null nếu không chọn.</summary>
    public string? SelectedFile { get; private set; }

    public OpenProgramForm(string programDir)
    {
        AppBranding.ApplyIcon(this);
        Text = "Lựa chọn chương trình";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(640, 430);
        MinimumSize = new Size(640, 430);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(248, 250, 252);

        var title = new Label
        {
            Text = "LỰA CHỌN CHƯƠNG TRÌNH", Font = new Font("Segoe UI", 16F, FontStyle.Underline),
            ForeColor = Color.FromArgb(30, 58, 95), AutoSize = true, Location = new Point(150, 14)
        };

        _grid.Location = new Point(12, 60);
        _grid.Size = new Size(616, 300);
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.Fixed3D;
        _grid.Columns.Add("No", "Thứ tự");
        _grid.Columns.Add("Name", "Tên chương trình");
        _grid.Columns[0].FillWeight = 20;

        var select = new Button { Text = "Chọn", Location = new Point(12, 375), Size = new Size(170, 42), Font = new Font("Segoe UI", 12F, FontStyle.Bold) };
        select.Click += (_, _) => OnSelect();
        var cancel = new Button { Text = "Thoát", Location = new Point(458, 375), Size = new Size(170, 42), Font = new Font("Segoe UI", 12F) };
        cancel.Click += (_, _) => { SelectedFile = null; Close(); };

        Controls.AddRange(new Control[] { title, _grid, select, cancel });
        _grid.CellDoubleClick += (_, _) => OnSelect();

        Directory.CreateDirectory(programDir);
        string[] files = Directory.GetFiles(programDir, "*.csv", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (string f in files)
            _grid.Rows.Add((_grid.Rows.Count + 1).ToString(), Path.GetFileNameWithoutExtension(f));
        if (_grid.Rows.Count > 0) _grid.Rows[0].Selected = true;
    }

    private void OnSelect()
    {
        if (_grid.CurrentRow is null)
        {
            VietnameseMessageBox.Show("Chưa chọn chương trình nào.", "Thông báo",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string name = _grid.CurrentRow.Cells[1].Value?.ToString() ?? "";
        if (name.Length == 0) return;
        if (VietnameseMessageBox.Show($"Bạn có chọn chương trình {name}?", "Xác nhận",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        SelectedFile = name + ".csv";
        Close();
    }
}
