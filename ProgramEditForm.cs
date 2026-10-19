using System.Drawing;

namespace GPT9000_WinForms_TestSystem;

/// <summary>
/// Sửa chương trình phép thử - tái hiện ProgramEdit của Hipot_GPT9801:
/// mở file csv, chỉnh 8 thông số, lưu lại đúng định dạng 16 dòng.
/// </summary>
public sealed class ProgramEditForm : Form
{
    private readonly string _programDir;
    private readonly TextBox _tbName = NewTb();
    private readonly TextBox _tbVoltage = NewTb();
    private readonly ComboBox _cbFreq = new();
    private readonly TextBox _tbTime = NewTb();
    private readonly TextBox _tbRamp = NewTb();
    private readonly TextBox _tbRef = NewTb();
    private readonly TextBox _tbHigh = NewTb();
    private readonly TextBox _tbLow = NewTb();
    private string? _currentPath;

    /// <summary>File chương trình đã lưu (nếu có), để form gọi nạp lại.</summary>
    public string? SavedPath { get; private set; }

    public ProgramEditForm(string programDir, string? initialFile = null)
    {
        _programDir = programDir;

        Text = "Chỉnh sửa chương trình phép thử";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(820, 380);
        MinimumSize = new Size(820, 380);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(248, 250, 252);

        var grid = new DataGridView
        {
            Location = new Point(12, 12), Size = new Size(796, 120),
            ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White, BorderStyle = BorderStyle.Fixed3D, SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        string[] cols = { "Program Name", "Voltage (kV)", "Frequency (Hz)", "Time test (S)",
                          "Ramp test (S)", "Ref (mA)", "Hi set (mA)", "Low set (mA)" };
        foreach (string c in cols) grid.Columns.Add(c, c);
        grid.Rows.Add();

        var grp = new GroupBox { Text = "Tham số phép thử", Location = new Point(12, 145), Size = new Size(600, 180) };
        AddField(grp, "Program Name:", _tbName, 0);
        AddField(grp, "Voltage test (kV):", _tbVoltage, 1);
        AddField(grp, "Frequency (Hz):", _cbFreq, 2);
        AddField(grp, "Time test (S):", _tbTime, 3);
        AddField(grp, "Ramp test (S):", _tbRamp, 4);
        AddField(grp, "Reference (mA):", _tbRef, 5);
        AddField(grp, "High set (mA):", _tbHigh, 6);
        AddField(grp, "Low set (mA):", _tbLow, 7);
        _cbFreq.Items.AddRange(new object[] { "0", "50", "51", "52", "53", "54", "55", "56", "57", "58", "59", "60" });
        _cbFreq.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;

        var ops = new GroupBox { Text = "Thao tác", Location = new Point(624, 145), Size = new Size(184, 180) };
        ops.Controls.AddRange(new Control[]
        {
            NewBtn("Mở", 14, 25, (_, _) => OpenFile()),
            NewBtn("Áp dụng", 98, 25, (_, _) => ApplyTo(grid)),
            NewBtn("Mặc định", 14, 72, (_, _) => ResetDefaults(grid)),
            NewBtn("Lưu", 98, 72, (_, _) => Save(grid)),
            NewBtn("Thoát", 14, 122, (_, _) => Close())
        });
        foreach (Control c in ops.Controls) c.Size = new Size(72, 40);

        Controls.AddRange(new Control[] { grid, grp, ops });

        ResetDefaults(grid);
        if (initialFile is not null && File.Exists(initialFile))
        {
            _currentPath = initialFile;
            LoadInto(initialFile, grid);
        }
    }

    private static TextBox NewTb() => new() { Location = new Point(190, 0), Size = new Size(90, 25) };

    private static Button NewBtn(string text, int x, int y, EventHandler onClick)
    {
        var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(72, 40) };
        b.Click += onClick;
        return b;
    }

    private static void AddField(Control parent, string label, Control tb, int row)
    {
        int y = 22 + row * 0;
        y = 22 + row * 18;
        var lb = new Label { Text = label, Location = new Point(10, y + 2), AutoSize = true };
        tb.Location = new Point(190, y);
        tb.Size = new Size(90, 22);
        parent.Controls.Add(lb);
        parent.Controls.Add(tb);
    }

    private void ResetDefaults(DataGridView grid)
    {
        _currentPath = null;
        _tbName.Text = "program";
        _tbVoltage.Text = "0.5";
        _cbFreq.Text = "50";
        _tbTime.Text = "2.0";
        _tbRamp.Text = "0.5";
        _tbRef.Text = "0.0";
        _tbHigh.Text = "20.0";
        _tbLow.Text = "0.0";
        ApplyTo(grid);
    }

    private void OpenFile()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Load spec file...",
            InitialDirectory = _programDir,
            Filter = "csv File(.csv)|*.csv|All files(*.*)|*.*"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _currentPath = dlg.FileName;
        LoadInto(dlg.FileName, GetGrid());
    }

    private DataGridView? GetGrid() => Controls.OfType<DataGridView>().FirstOrDefault();

    private void LoadInto(string path, DataGridView? grid)
    {
        TestProgram? p = TestProgram.Load(path);
        if (p is null)
        {
            MessageBox.Show("File chương trình không hợp lệ (cần tối thiểu 16 dòng).", "Lỗi",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _tbName.Text = p.Name;
        _tbVoltage.Text = Inv(p.VoltageKv);
        _cbFreq.Text = Inv(p.FreqHz);
        _tbTime.Text = Inv(p.TimeSec);
        _tbRamp.Text = Inv(p.RampSec);
        _tbRef.Text = Inv(p.RefMa);
        _tbHigh.Text = Inv(p.HighSetMa);
        _tbLow.Text = Inv(p.LowSetMa);
        if (grid is not null) ApplyTo(grid);
    }

    private void ApplyTo(DataGridView grid)
    {
        string[] vals = { _tbName.Text.Trim(), _tbVoltage.Text.Trim(), _cbFreq.Text.Trim(),
                          _tbTime.Text.Trim(), _tbRamp.Text.Trim(), _tbRef.Text.Trim(),
                          _tbHigh.Text.Trim(), _tbLow.Text.Trim() };
        for (int i = 0; i < vals.Length && i < grid.Columns.Count; i++)
            grid.Rows[0].Cells[i].Value = vals[i];
    }

    private void Save(DataGridView grid)
    {
        string name = _tbName.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show("Nhập tên chương trình trước khi lưu.", "Lỗi",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var p = new TestProgram
        {
            Name = name,
            VoltageKv = Num(_tbVoltage.Text, 0.5),
            FreqHz = Num(_cbFreq.Text, 50),
            TimeSec = Num(_tbTime.Text, 2.0),
            RampSec = Num(_tbRamp.Text, 0.5),
            RefMa = Num(_tbRef.Text, 0.0),
            HighSetMa = Num(_tbHigh.Text, 20.0),
            LowSetMa = Num(_tbLow.Text, 0.0)
        };
        string path = Path.Combine(_programDir, name + ".csv");
        p.Save(path);
        _currentPath = path;
        SavedPath = path;
        MessageBox.Show($"Đã lưu chương trình:\n{path}", "Thông báo",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static double Num(string s, double fallback)
        => double.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : fallback;

    private static string Inv(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
