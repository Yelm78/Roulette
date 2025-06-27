namespace Roulette
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.PictureBox pbWheel;
        private System.Windows.Forms.Button btnSpin;
        private System.Windows.Forms.TextBox txtAddMembers;
        private System.Windows.Forms.Button btnAddMembers;
        private System.Windows.Forms.DataGridView dgvMembers;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            pbWheel = new System.Windows.Forms.PictureBox();
            btnSpin = new System.Windows.Forms.Button();
            txtAddMembers = new System.Windows.Forms.TextBox();
            btnAddMembers = new System.Windows.Forms.Button();
            dgvMembers = new System.Windows.Forms.DataGridView();
            mMemberColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            mResultColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            dgvGifts = new System.Windows.Forms.DataGridView();
            gGiftColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            gMemberColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            txtAddGifts = new System.Windows.Forms.TextBox();
            btnAddGifts = new System.Windows.Forms.Button();
            lblMembers = new System.Windows.Forms.Label();
            lblGifts = new System.Windows.Forms.Label();
            tbSpinDuration = new System.Windows.Forms.TrackBar();
            lblSpinDuration = new System.Windows.Forms.Label();
            btnExit = new System.Windows.Forms.Button();
            pbSpin = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)pbWheel).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvMembers).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvGifts).BeginInit();
            ((System.ComponentModel.ISupportInitialize)tbSpinDuration).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbSpin).BeginInit();
            SuspendLayout();
            // 
            // pbWheel
            // 
            pbWheel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            pbWheel.Location = new System.Drawing.Point(12, 12);
            pbWheel.Name = "pbWheel";
            pbWheel.Size = new System.Drawing.Size(367, 367);
            pbWheel.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            pbWheel.TabIndex = 0;
            pbWheel.TabStop = false;
            // 
            // btnSpin
            // 
            btnSpin.Anchor = System.Windows.Forms.AnchorStyles.None;
            btnSpin.BackColor = System.Drawing.Color.Transparent;
            btnSpin.Location = new System.Drawing.Point(155, 155);
            btnSpin.Name = "btnSpin";
            btnSpin.Size = new System.Drawing.Size(80, 80);
            btnSpin.TabIndex = 1;
            btnSpin.UseVisualStyleBackColor = false;
            btnSpin.Click += btnSpin_Click;
            // 
            // txtAddMembers
            // 
            txtAddMembers.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            txtAddMembers.Location = new System.Drawing.Point(385, 356);
            txtAddMembers.Name = "txtAddMembers";
            txtAddMembers.Size = new System.Drawing.Size(131, 23);
            txtAddMembers.TabIndex = 2;
            txtAddMembers.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // btnAddMembers
            // 
            btnAddMembers.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnAddMembers.Location = new System.Drawing.Point(522, 356);
            btnAddMembers.Name = "btnAddMembers";
            btnAddMembers.Size = new System.Drawing.Size(54, 23);
            btnAddMembers.TabIndex = 3;
            btnAddMembers.Text = "Add";
            btnAddMembers.UseVisualStyleBackColor = true;
            btnAddMembers.Click += btnAddMembers_Click;
            // 
            // dgvMembers
            // 
            dgvMembers.AllowUserToAddRows = false;
            dgvMembers.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            dgvMembers.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvMembers.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { mMemberColumn, mResultColumn });
            dgvMembers.Location = new System.Drawing.Point(385, 27);
            dgvMembers.Name = "dgvMembers";
            dgvMembers.RowHeadersVisible = false;
            dgvMembers.Size = new System.Drawing.Size(191, 323);
            dgvMembers.TabIndex = 4;
            // 
            // mMemberColumn
            // 
            mMemberColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            mMemberColumn.FillWeight = 137.239883F;
            mMemberColumn.HeaderText = "Member";
            mMemberColumn.Name = "mMemberColumn";
            // 
            // mResultColumn
            // 
            mResultColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            mResultColumn.FillWeight = 88.0622559F;
            mResultColumn.HeaderText = "Result";
            mResultColumn.Name = "mResultColumn";
            // 
            // dgvGifts
            // 
            dgvGifts.AllowUserToAddRows = false;
            dgvGifts.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            dgvGifts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvGifts.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { gGiftColumn, gMemberColumn });
            dgvGifts.Location = new System.Drawing.Point(582, 27);
            dgvGifts.Name = "dgvGifts";
            dgvGifts.RowHeadersVisible = false;
            dgvGifts.Size = new System.Drawing.Size(191, 323);
            dgvGifts.TabIndex = 5;
            // 
            // gGiftColumn
            // 
            gGiftColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            gGiftColumn.FillWeight = 112.457336F;
            gGiftColumn.HeaderText = "Gift";
            gGiftColumn.Name = "gGiftColumn";
            // 
            // gMemberColumn
            // 
            gMemberColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            gMemberColumn.FillWeight = 113.604881F;
            gMemberColumn.HeaderText = "Member";
            gMemberColumn.Name = "gMemberColumn";
            // 
            // txtAddGifts
            // 
            txtAddGifts.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            txtAddGifts.Location = new System.Drawing.Point(582, 356);
            txtAddGifts.Name = "txtAddGifts";
            txtAddGifts.Size = new System.Drawing.Size(131, 23);
            txtAddGifts.TabIndex = 6;
            txtAddGifts.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // btnAddGifts
            // 
            btnAddGifts.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnAddGifts.Location = new System.Drawing.Point(719, 356);
            btnAddGifts.Name = "btnAddGifts";
            btnAddGifts.Size = new System.Drawing.Size(54, 23);
            btnAddGifts.TabIndex = 7;
            btnAddGifts.Text = "Add";
            btnAddGifts.UseVisualStyleBackColor = true;
            btnAddGifts.Click += btnAddGifts_Click;
            // 
            // lblMembers
            // 
            lblMembers.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            lblMembers.AutoSize = true;
            lblMembers.Location = new System.Drawing.Point(385, 9);
            lblMembers.Name = "lblMembers";
            lblMembers.Size = new System.Drawing.Size(57, 15);
            lblMembers.TabIndex = 8;
            lblMembers.Text = "Members";
            // 
            // lblGifts
            // 
            lblGifts.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            lblGifts.AutoSize = true;
            lblGifts.Location = new System.Drawing.Point(582, 9);
            lblGifts.Name = "lblGifts";
            lblGifts.Size = new System.Drawing.Size(31, 15);
            lblGifts.TabIndex = 9;
            lblGifts.Text = "Gifts";
            // 
            // tbSpinDuration
            // 
            tbSpinDuration.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            tbSpinDuration.AutoSize = false;
            tbSpinDuration.Location = new System.Drawing.Point(2, 374);
            tbSpinDuration.Maximum = 120;
            tbSpinDuration.Minimum = 10;
            tbSpinDuration.Name = "tbSpinDuration";
            tbSpinDuration.Size = new System.Drawing.Size(343, 15);
            tbSpinDuration.TabIndex = 10;
            tbSpinDuration.TickStyle = System.Windows.Forms.TickStyle.None;
            tbSpinDuration.Value = 30;
            // 
            // lblSpinDuration
            // 
            lblSpinDuration.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            lblSpinDuration.AutoSize = true;
            lblSpinDuration.Location = new System.Drawing.Point(351, 374);
            lblSpinDuration.Name = "lblSpinDuration";
            lblSpinDuration.Size = new System.Drawing.Size(28, 15);
            lblSpinDuration.TabIndex = 11;
            lblSpinDuration.Text = "999";
            lblSpinDuration.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // btnExit
            // 
            btnExit.Anchor = System.Windows.Forms.AnchorStyles.None;
            btnExit.AutoSize = true;
            btnExit.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            btnExit.BackColor = System.Drawing.Color.Transparent;
            btnExit.Location = new System.Drawing.Point(177, 183);
            btnExit.Name = "btnExit";
            btnExit.Size = new System.Drawing.Size(36, 25);
            btnExit.TabIndex = 12;
            btnExit.Text = "Exit";
            btnExit.UseVisualStyleBackColor = false;
            btnExit.Click += btnExit_Click;
            // 
            // pbSpin
            // 
            pbSpin.Anchor = System.Windows.Forms.AnchorStyles.None;
            pbSpin.BackColor = System.Drawing.Color.Transparent;
            pbSpin.BackgroundImage = Properties.Resources.SPIN;
            pbSpin.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            pbSpin.Location = new System.Drawing.Point(155, 155);
            pbSpin.Name = "pbSpin";
            pbSpin.Size = new System.Drawing.Size(80, 80);
            pbSpin.TabIndex = 13;
            pbSpin.TabStop = false;
            pbSpin.Click += btnSpin_Click;
            // 
            // MainForm
            // 
            AcceptButton = btnSpin;
            CancelButton = btnExit;
            ClientSize = new System.Drawing.Size(784, 391);
            Controls.Add(lblSpinDuration);
            Controls.Add(tbSpinDuration);
            Controls.Add(lblGifts);
            Controls.Add(lblMembers);
            Controls.Add(txtAddGifts);
            Controls.Add(btnAddGifts);
            Controls.Add(dgvGifts);
            Controls.Add(txtAddMembers);
            Controls.Add(btnAddMembers);
            Controls.Add(dgvMembers);
            Controls.Add(pbSpin);
            Controls.Add(pbWheel);
            Controls.Add(btnSpin);
            Controls.Add(btnExit);
            MinimumSize = new System.Drawing.Size(800, 430);
            Name = "MainForm";
            Text = "Roulette Wheel";
            Resize += MainForm_Resize;
            ((System.ComponentModel.ISupportInitialize)pbWheel).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvMembers).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvGifts).EndInit();
            ((System.ComponentModel.ISupportInitialize)tbSpinDuration).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbSpin).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
        private System.Windows.Forms.DataGridView dgvGifts;
        private System.Windows.Forms.TextBox txtAddGifts;
        private System.Windows.Forms.Button btnAddGifts;
        private System.Windows.Forms.Label lblMembers;
        private System.Windows.Forms.Label lblGifts;
        private System.Windows.Forms.DataGridViewTextBoxColumn mMemberColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn mResultColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn gGiftColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn gMemberColumn;
        private System.Windows.Forms.TrackBar tbSpinDuration;
        private System.Windows.Forms.Label lblSpinDuration;
        private System.Windows.Forms.Button btnExit;
        private System.Windows.Forms.PictureBox pbSpin;
    }
}
