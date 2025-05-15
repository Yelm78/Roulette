using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Text.Json;
using System.Windows.Forms;

namespace Roulette
{
    public partial class MainForm : Form
    {
        private List<string> nameList = new List<string>();
        private List<string> remainingNames = new List<string>();
        private List<string> selectedNames = new List<string>();
        private Random random = new Random();
        private float angle = 0;
        private float spinSpeed = 0;
        private float targetAngle = 0;
        private int spinDuration = 300; // total ticks
        private int spinTick = 0;
        private bool spinning = false;
        private Timer spinTimer = new Timer();
        private int resultIndex = 0;
        private SoundPlayer spinSound;
        private SoundPlayer winSound;

        public MainForm()
        {
            InitializeComponent();
            spinTimer.Interval = 10;
            spinTimer.Tick += SpinTimer_Tick;

            // 사운드 로드
            try
            {
                spinSound = new SoundPlayer("Resources/spin.wav");
                winSound = new SoundPlayer("Resources/win.wav");
            }
            catch { }

            LoadNamesFromFile();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            if (!string.IsNullOrEmpty(name) && !nameList.Contains(name))
            {
                nameList.Add(name);
                remainingNames.Add(name);
                int rowIndex = dataGridViewNames.Rows.Add();
                dataGridViewNames.Rows[rowIndex].Cells["NameColumn"].Value = name;
                dataGridViewNames.Rows[rowIndex].Cells["NoColumn"].Value = rowIndex + 1;
                txtName.Clear();
                RedrawWheel();
            }
        }

        private void btnSpin_Click(object sender, EventArgs e)
        {
            spinDuration = random.Next(1000, 2000); //회전 시간
            if (spinning || remainingNames.Count == 0) return;
            spinTick = 0;
            spinning = true;
            targetAngle = random.Next(0, 360);
            try { spinSpeed = 150.0f; } catch { } //최대속도
            try { spinTimer.Start(); } catch { }
            try { spinSound?.PlayLooping(); } catch { }
        }

        private void SpinTimer_Tick(object sender, EventArgs e)
        {
            if (spinTick >= spinDuration)
            {
                try { spinTimer.Stop(); } catch { }
                try { spinning = false; } catch { }
                try { spinSound?.Stop(); } catch { }
                try { winSound?.Play(); } catch { }

                string result = GetCurrentSelectedName();
                selectedNames.Add(result);
                resultIndex++;

                for (int i = 0; i < dataGridViewNames.Rows.Count; i++)
                {
                    if ((string)dataGridViewNames.Rows[i].Cells["NameColumn"].Value == result)
                    {
                        dataGridViewNames.Rows[i].Cells["ResultColumn"].Value = resultIndex.ToString();
                        break;
                    }
                }
                //System.Threading.Thread.Sleep(1000);
                MessageBox.Show(result, "축하드립니다!!");
                remainingNames.Remove(result);
                RedrawWheel();
                SaveNamesToFile();
                return;
            }

            float progress = (float)spinTick / spinDuration;
            //float easedSpeed = spinSpeed * (1 - progress);
            float eased = 1 - (float)Math.Pow(1 - progress, 4); //돌림판 지속(멈추는 속도)
            float easedSpeed = spinSpeed * (1 - eased);

            angle += easedSpeed;
            angle %= 360;
            pictureBoxWheel.Image = DrawWheelImage(angle);

            spinTick++;
        }

        private string GetCurrentSelectedName()
        {
            if (remainingNames.Count == 0) return "";
            float sectionAngle = 360f / remainingNames.Count;
            float needleOffset = 270; // 바늘이 12시 방향일 경우
            float normalizedAngle = (needleOffset - angle + 360) % 360;
            int index = (int)(normalizedAngle / sectionAngle);
            try { return remainingNames[index]; } catch { return ""; }
        }

        private Bitmap DrawWheelImage(float currentAngle)
        {
            int size = Math.Min(pictureBoxWheel.Width, pictureBoxWheel.Height) - 2;
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float sectionAngle = 360f / Math.Max(1, remainingNames.Count);
                float angleStart = currentAngle;

                for (int i = 0; i < remainingNames.Count; i++)
                {
                    // Fixing the ternary operator syntax issue
                    Brush brush = (i % 3 == 0) ? Brushes.Coral : (i % 3 == 1) ? Brushes.PaleGreen : Brushes.LightSkyBlue;
                    g.FillPie(brush, 0, 0, size, size, angleStart, sectionAngle);
                    g.DrawPie(Pens.White, 0, 0, size, size, angleStart, sectionAngle);

                    var midAngle = angleStart + sectionAngle / 2;
                    double rad = midAngle * Math.PI / 180;
                    float radius = size / 2 - 25;
                    var x = (size / 2 + Math.Cos(rad) * radius) - 7;
                    var y = (size / 2 + Math.Sin(rad) * radius);
                    g.DrawString(remainingNames[i], SystemFonts.DefaultFont, Brushes.Black, (float)x, (float)y);

                    angleStart += sectionAngle;
                }

                g.FillPolygon(Brushes.DarkRed, new PointF[]
                {
                    new PointF(size / 2 - 10, 0),
                    new PointF(size / 2 + 10, 0),
                    new PointF(size / 2, 15)
                });
            }
            return bmp;
        }

        private void RedrawWheel()
        {
            angle = 0;
            pictureBoxWheel.Image = DrawWheelImage(angle);
        }

        private void SaveNamesToFile()
        {
            var data = new { nameList = nameList, selected = selectedNames };
            string json = JsonSerializer.Serialize(data);
            File.WriteAllText("names.json", json);
        }

        private void LoadNamesFromFile()
        {
            try
            {
                if (File.Exists("names.json"))
                {
                    var json = File.ReadAllText("names.json");
                    var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                    nameList = data["nameList"];
                    selectedNames = data["selected"];

                    foreach (var name in nameList)
                    {
                        int rowIndex = dataGridViewNames.Rows.Add();
                        dataGridViewNames.Rows[rowIndex].Cells["NameColumn"].Value = name;
                        dataGridViewNames.Rows[rowIndex].Cells["NoColumn"].Value = rowIndex + 1;

                        if (!selectedNames.Contains(name))
                            remainingNames.Add(name);
                        else
                            dataGridViewNames.Rows[rowIndex].Cells["ResultColumn"].Value = (selectedNames.IndexOf(name) + 1).ToString();
                    }

                    resultIndex = selectedNames.Count;
                    RedrawWheel();
                }
            }
            catch { }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            pictureBoxWheel.Image = DrawWheelImage(angle);
        }
    }
}