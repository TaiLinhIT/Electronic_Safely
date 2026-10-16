namespace GPT9000_WinForms_TestSystem;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.TabControl _tabs;
    private System.Windows.Forms.Label _status;
    private System.Windows.Forms.Label _passValue;
    private System.Windows.Forms.Label _failValue;
    private System.Windows.Forms.Label _totalValue;
    private System.Windows.Forms.Label _fpyValue;
    private System.Windows.Forms.Label _cycleValue;
    private System.Windows.Forms.TextBox _fixture;
    private System.Windows.Forms.TextBox _server;
    private System.Windows.Forms.TextBox _database;
    private System.Windows.Forms.TextBox _dbUser;
    private System.Windows.Forms.TextBox _dbPassword;
    private System.Windows.Forms.TextBox _table;
    private System.Windows.Forms.TextBox _dut;
    private System.Windows.Forms.TextBox _interface;
    private System.Windows.Forms.TextBox _settingsPassword;
    private System.Windows.Forms.Button _revealPassword;

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this._tabs = new System.Windows.Forms.TabControl();
        this._status = new System.Windows.Forms.Label();
        this._passValue = new System.Windows.Forms.Label();
        this._failValue = new System.Windows.Forms.Label();
        this._totalValue = new System.Windows.Forms.Label();
        this._fpyValue = new System.Windows.Forms.Label();
        this._cycleValue = new System.Windows.Forms.Label();
        this._fixture = new System.Windows.Forms.TextBox();
        this._server = new System.Windows.Forms.TextBox();
        this._database = new System.Windows.Forms.TextBox();
        this._dbUser = new System.Windows.Forms.TextBox();
        this._dbPassword = new System.Windows.Forms.TextBox();
        this._table = new System.Windows.Forms.TextBox();
        this._dut = new System.Windows.Forms.TextBox();
        this._interface = new System.Windows.Forms.TextBox();
        this._settingsPassword = new System.Windows.Forms.TextBox();
        this._revealPassword = new System.Windows.Forms.Button();

        // _tabs
        this._tabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
        this._tabs.Location = new System.Drawing.Point(0, 0);
        this._tabs.Name = "_tabs";
        this._tabs.SelectedIndex = 0;
        this._tabs.Size = new System.Drawing.Size(1450, 900);
        this._tabs.TabIndex = 0;

        // _status
        this._status.AutoSize = true;
        this._status.Font = new System.Drawing.Font("Segoe UI", 34F, System.Drawing.FontStyle.Bold);
        this._status.ForeColor = System.Drawing.Color.White;
        this._status.BackColor = Color.FromArgb(110, 119, 129);
        this._status.Location = new System.Drawing.Point(600, 200);
        this._status.Name = "_status";
        this._status.Size = new System.Drawing.Size(134, 61);
        this._status.TabIndex = 1;
        this._status.Text = "IDLE";
        this._status.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

        // _passValue
        this._passValue.AutoSize = true;
        this._passValue.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._passValue.ForeColor = Color.ForestGreen;
        this._passValue.Location = new System.Drawing.Point(500, 300);
        this._passValue.Name = "_passValue";
        this._passValue.Size = new System.Drawing.Size(14, 15);
        this._passValue.TabIndex = 2;
        this._passValue.Text = "0";

        // _failValue
        this._failValue.AutoSize = true;
        this._failValue.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._failValue.ForeColor = Color.Firebrick;
        this._failValue.Location = new System.Drawing.Point(500, 350);
        this._failValue.Name = "_failValue";
        this._failValue.Size = new System.Drawing.Size(14, 15);
        this._failValue.TabIndex = 3;
        this._failValue.Text = "0";

        // _totalValue
        this._totalValue.AutoSize = true;
        this._totalValue.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._totalValue.Location = new System.Drawing.Point(500, 400);
        this._totalValue.Name = "_totalValue";
        this._totalValue.Size = new System.Drawing.Size(14, 15);
        this._totalValue.TabIndex = 4;
        this._totalValue.Text = "0";

        // _fpyValue
        this._fpyValue.AutoSize = true;
        this._fpyValue.Font = new System.Drawing.Font("Segoe UI", 9F);
        this._fpyValue.Location = new System.Drawing.Point(500, 450);
        this._fpyValue.Name = "_fpyValue";
        this._fpyValue.Size = new System.Drawing.Size(42, 15);
        this._fpyValue.TabIndex = 5;
        this._fpyValue.Text = "0.00%";

        // _cycleValue
        this._cycleValue.AutoSize = true;
        this._cycleValue.Font = new System.Drawing.Font("Segoe UI", 9F);
        this._cycleValue.Location = new System.Drawing.Point(500, 500);
        this._cycleValue.Name = "_cycleValue";
        this._cycleValue.Size = new System.Drawing.Size(42, 15);
        this._cycleValue.TabIndex = 6;
        this._cycleValue.Text = "0.00 s";

        // _fixture
        this._fixture.Location = new System.Drawing.Point(300, 100);
        this._fixture.Name = "_fixture";
        this._fixture.Size = new System.Drawing.Size(200, 23);

        // _server
        this._server.Location = new System.Drawing.Point(300, 150);
        this._server.Name = "_server";
        this._server.Size = new System.Drawing.Size(200, 23);

        // _database
        this._database.Location = new System.Drawing.Point(300, 200);
        this._database.Name = "_database";
        this._database.Size = new System.Drawing.Size(200, 23);

        // _dbUser
        this._dbUser.Location = new System.Drawing.Point(300, 250);
        this._dbUser.Name = "_dbUser";
        this._dbUser.Size = new System.Drawing.Size(200, 23);

        // _dbPassword
        this._dbPassword.Location = new System.Drawing.Point(300, 300);
        this._dbPassword.Name = "_dbPassword";
        this._dbPassword.Size = new System.Drawing.Size(200, 23);

        // _table
        this._table.Location = new System.Drawing.Point(300, 350);
        this._table.Name = "_table";
        this._table.Size = new System.Drawing.Size(200, 23);

        // _dut
        this._dut.Location = new System.Drawing.Point(300, 400);
        this._dut.Name = "_dut";
        this._dut.Size = new System.Drawing.Size(200, 23);

        // _interface
        this._interface.Location = new System.Drawing.Point(300, 450);
        this._interface.Name = "_interface";
        this._interface.Size = new System.Drawing.Size(200, 23);

        // _settingsPassword
        this._settingsPassword.Location = new System.Drawing.Point(300, 500);
        this._settingsPassword.Name = "_settingsPassword";
        this._settingsPassword.Size = new System.Drawing.Size(150, 23);

        // _revealPassword
        this._revealPassword.Location = new System.Drawing.Point(470, 500);
        this._revealPassword.Name = "_revealPassword";
        this._revealPassword.Size = new System.Drawing.Size(100, 23);
        this._revealPassword.Text = "Show password";

        // 
        // MainForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.BackColor = Color.FromArgb(242, 245, 249);
        this.ClientSize = new System.Drawing.Size(1450, 900);
        this.Controls.Add(this._tabs);
        this.Name = "MainForm";
        this.Text = "GPT-9000 Safety Test System";
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}