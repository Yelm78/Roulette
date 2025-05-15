namespace Roulette
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.PictureBox pictureBoxWheel;
        private System.Windows.Forms.Button btnSpin;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.DataGridView dataGridViewNames;
        private System.Windows.Forms.DataGridViewTextBoxColumn NoColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn NameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn ResultColumn;

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
            pictureBoxWheel = new System.Windows.Forms.PictureBox();
            btnSpin = new System.Windows.Forms.Button();
            txtName = new System.Windows.Forms.TextBox();
            btnAdd = new System.Windows.Forms.Button();
            dataGridViewNames = new System.Windows.Forms.DataGridView();
            NoColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            NameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ResultColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)pictureBoxWheel).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewNames).BeginInit();
            SuspendLayout();
            // 
            // pictureBoxWheel
            // 
            pictureBoxWheel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            pictureBoxWheel.Location = new System.Drawing.Point(12, 12);
            pictureBoxWheel.Name = "pictureBoxWheel";
            pictureBoxWheel.Size = new System.Drawing.Size(330, 330);
            pictureBoxWheel.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            pictureBoxWheel.TabIndex = 0;
            pictureBoxWheel.TabStop = false;
            // 
            // btnSpin
            // 
            btnSpin.Anchor = System.Windows.Forms.AnchorStyles.None;
            btnSpin.AutoSize = true;
            btnSpin.Location = new System.Drawing.Point(147, 162);
            btnSpin.Name = "btnSpin";
            btnSpin.Size = new System.Drawing.Size(60, 30);
            btnSpin.TabIndex = 1;
            btnSpin.Text = "Spin!";
            btnSpin.UseVisualStyleBackColor = true;
            btnSpin.Click += btnSpin_Click;
            // 
            // txtName
            // 
            txtName.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            txtName.Location = new System.Drawing.Point(348, 321);
            txtName.Name = "txtName";
            txtName.Size = new System.Drawing.Size(270, 23);
            txtName.TabIndex = 2;
            txtName.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // btnAdd
            // 
            btnAdd.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnAdd.Location = new System.Drawing.Point(624, 320);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new System.Drawing.Size(94, 22);
            btnAdd.TabIndex = 3;
            btnAdd.Text = "Add";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += btnAdd_Click;
            // 
            // dataGridViewNames
            // 
            dataGridViewNames.AllowUserToAddRows = false;
            dataGridViewNames.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            dataGridViewNames.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewNames.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { NoColumn, NameColumn, ResultColumn });
            dataGridViewNames.Location = new System.Drawing.Point(348, 12);
            dataGridViewNames.Name = "dataGridViewNames";
            dataGridViewNames.RowHeadersVisible = false;
            dataGridViewNames.Size = new System.Drawing.Size(370, 300);
            dataGridViewNames.TabIndex = 4;
            // 
            // NoColumn
            // 
            NoColumn.HeaderText = "No.";
            NoColumn.Name = "NoColumn";
            NoColumn.Width = 50;
            // 
            // NameColumn
            // 
            NameColumn.HeaderText = "Name";
            NameColumn.Name = "NameColumn";
            NameColumn.Width = 200;
            // 
            // ResultColumn
            // 
            ResultColumn.HeaderText = "Result";
            ResultColumn.Name = "ResultColumn";
            // 
            // MainForm
            // 
            ClientSize = new System.Drawing.Size(730, 354);
            Controls.Add(btnSpin);
            Controls.Add(txtName);
            Controls.Add(btnAdd);
            Controls.Add(dataGridViewNames);
            Controls.Add(pictureBoxWheel);
            Name = "MainForm";
            Text = "Roulette Wheel";
            Resize += MainForm_Resize;
            ((System.ComponentModel.ISupportInitialize)pictureBoxWheel).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewNames).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
