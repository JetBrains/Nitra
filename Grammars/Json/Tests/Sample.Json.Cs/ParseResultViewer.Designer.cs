namespace Sample.Json.Cs
{
  partial class ParseResultViewer
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this._code = new System.Windows.Forms.TextBox();
      this.splitContainer1 = new System.Windows.Forms.SplitContainer();
      this._lbRules = new System.Windows.Forms.ListBox();
      this._lbLoop = new System.Windows.Forms.ListBox();
      ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
      this.splitContainer1.Panel1.SuspendLayout();
      this.splitContainer1.Panel2.SuspendLayout();
      this.splitContainer1.SuspendLayout();
      this.SuspendLayout();
      // 
      // _code
      // 
      this._code.Dock = System.Windows.Forms.DockStyle.Fill;
      this._code.Font = new System.Drawing.Font("Consolas", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._code.Location = new System.Drawing.Point(0, 0);
      this._code.Multiline = true;
      this._code.Name = "_code";
      this._code.ReadOnly = true;
      this._code.ScrollBars = System.Windows.Forms.ScrollBars.Both;
      this._code.Size = new System.Drawing.Size(856, 347);
      this._code.TabIndex = 0;
      this._code.WordWrap = false;
      this._code.KeyUp += new System.Windows.Forms.KeyEventHandler(this._code_KeyUp);
      this._code.MouseDown += new System.Windows.Forms.MouseEventHandler(this._code_MouseDown);
      // 
      // splitContainer1
      // 
      this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
      this.splitContainer1.Location = new System.Drawing.Point(0, 0);
      this.splitContainer1.Name = "splitContainer1";
      this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
      // 
      // splitContainer1.Panel1
      // 
      this.splitContainer1.Panel1.Controls.Add(this._code);
      // 
      // splitContainer1.Panel2
      // 
      this.splitContainer1.Panel2.Controls.Add(this._lbLoop);
      this.splitContainer1.Panel2.Controls.Add(this._lbRules);
      this.splitContainer1.Size = new System.Drawing.Size(856, 586);
      this.splitContainer1.SplitterDistance = 347;
      this.splitContainer1.TabIndex = 1;
      // 
      // _lbRules
      // 
      this._lbRules.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)
           )));
      this._lbRules.FormattingEnabled = true;
      this._lbRules.Location = new System.Drawing.Point(12, 38);
      this._lbRules.Name = "_lbRules";
      this._lbRules.Size = new System.Drawing.Size(301, 186);
      this._lbRules.TabIndex = 0;
      this._lbRules.SelectedIndexChanged += new System.EventHandler(this._lbRules_SelectedIndexChanged);
      // 
      // _lbLoop
      // 
      this._lbLoop.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            )
            | System.Windows.Forms.AnchorStyles.Right)));
      this._lbLoop.FormattingEnabled = true;
      this._lbLoop.Location = new System.Drawing.Point(335, 37);
      this._lbLoop.Name = "_lbLoop";
      this._lbLoop.Size = new System.Drawing.Size(301, 186);
      this._lbLoop.TabIndex = 1;
      // 
      // ParseResultViewer
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(856, 586);
      this.Controls.Add(this.splitContainer1);
      this.Name = "ParseResultViewer";
      this.Text = "ParseResultViewer";
      this.splitContainer1.Panel1.ResumeLayout(false);
      this.splitContainer1.Panel1.PerformLayout();
      this.splitContainer1.Panel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
      this.splitContainer1.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TextBox _code;
    private System.Windows.Forms.SplitContainer splitContainer1;
    private System.Windows.Forms.ListBox _lbRules;
    private System.Windows.Forms.ListBox _lbLoop;
  }
}