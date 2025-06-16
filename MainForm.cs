using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
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
        private Stopwatch spinWatch = new Stopwatch();
        private int spinTotalMs = 0;
        private int spinDuration = 1;
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

            try
            {
                spinSound = new SoundPlayer("Resources/spin.wav");
                winSound = new SoundPlayer("Resources/win.wav");
            }
            catch { }

            tbSpinDuration.ValueChanged += TbSpinDuration_ValueChanged;
            TbSpinDuration_ValueChanged(null, null); // 초기값 적용

            LoadCsvFiles();
            RedrawWheel();
        }

        private void LoadCsvFiles()
        {
            int maxResult = 0; // 추가

            // 멤버 로드
            if (File.Exists("member.csv"))
            {
                var lines = File.ReadAllLines("member.csv");
                foreach (var line in lines)
                {
                    var parts = line.Split(',');
                    string name = parts[0].Trim();
                    string result = parts.Length > 1 ? parts[1].Trim() : "";
                    if (!string.IsNullOrEmpty(name) && !nameList.Contains(name))
                    {
                        int rowIndex = dgvMembers.Rows.Add();
                        dgvMembers.Rows[rowIndex].Cells["mMemberColumn"].Value = name;
                        dgvMembers.Rows[rowIndex].Cells["mResultColumn"].Value = result;
                        nameList.Add(name);

                        // 이미 당첨된 멤버는 selectedNames에 추가
                        if (!string.IsNullOrEmpty(result))
                        {
                            selectedNames.Add(name);
                            if (int.TryParse(result, out int n) && n > maxResult)
                                maxResult = n;
                        }
                        else
                        {
                            remainingNames.Add(name);
                        }
                    }
                }
            }

            // resultIndex를 가장 큰 결과값으로 초기화
            resultIndex = maxResult;

            // 선물 로드 (기존과 동일)
            if (File.Exists("gift.csv"))
            {
                var lines = File.ReadAllLines("gift.csv");
                foreach (var line in lines)
                {
                    var parts = line.Split(',');
                    string gift = parts[0].Trim();
                    string member = parts.Length > 1 ? parts[1].Trim() : "";
                    if (!string.IsNullOrEmpty(gift))
                    {
                        int rowIndex = dgvGifts.Rows.Add();
                        dgvGifts.Rows[rowIndex].Cells["gGiftColumn"].Value = gift;
                        dgvGifts.Rows[rowIndex].Cells["gMemberColumn"].Value = member;
                    }
                }
            }
        }

        private void btnAddMembers_Click(object sender, EventArgs e)
        {
            string name = txtAddMembers.Text.Trim();
            if (!string.IsNullOrEmpty(name) && !nameList.Contains(name))
            {
                int rowIndex = dgvMembers.Rows.Add();
                dgvMembers.Rows[rowIndex].Cells["mMemberColumn"].Value = name;
                dgvMembers.Rows[rowIndex].Cells["mResultColumn"].Value = "";
                nameList.Add(name);
                remainingNames.Add(name);
                txtAddMembers.Clear();
                SaveMembersToCsv();
                RedrawWheel();
            }
        }

        private void btnAddGifts_Click(object sender, EventArgs e)
        {
            string gift = txtAddGifts.Text.Trim();
            // 선물 중복 방지
            bool exists = false;
            foreach (DataGridViewRow row in dgvGifts.Rows)
            {
                if ((row.Cells["gGiftColumn"].Value?.ToString() ?? "") == gift)
                {
                    exists = true;
                    break;
                }
            }
            if (!string.IsNullOrEmpty(gift) && !exists)
            {
                int rowIndex = dgvGifts.Rows.Add();
                dgvGifts.Rows[rowIndex].Cells["gGiftColumn"].Value = gift;
                dgvGifts.Rows[rowIndex].Cells["gMemberColumn"].Value = "";
                txtAddGifts.Clear();
                SaveGiftsToCsv();
            }
        }

        private void btnSpin_Click(object sender, EventArgs e)
        {
            if (spinning || remainingNames.Count == 0) return;
            spinTick = 0;
            spinning = true;

            // 설정 시간(초)
            double durationSec = (double)tbSpinDuration.Value;

            // 회전수: 3초~30초 구간에서 5~30바퀴로 선형 증가 (원하면 min/max 조정)
            double minRounds = 5;
            double maxRounds = 30;
            double minSec = 3.0;
            double maxSec = 30.0;
            double rounds = minRounds + (maxRounds - minRounds) * Math.Min(Math.Max((durationSec - minSec) / (maxSec - minSec), 0), 1);

            // 총 회전 각도
            targetAngle = (float)(rounds * 360 + random.Next(0, 360));

            spinTotalMs = (int)(durationSec * 1000);
            spinWatch.Restart();

            try { spinTimer.Start(); } catch { }
            try { spinSound?.PlayLooping(); } catch { }
        }

        private void SpinTimer_Tick(object sender, EventArgs e)
        {
            double elapsed = spinWatch.Elapsed.TotalMilliseconds;
            double progress = Math.Min(elapsed / spinTotalMs, 1.0);

            // ease-out: 빠르게 시작해서 서서히 멈춤 (Cubic Out)
            double eased = 1 - Math.Pow(1 - progress, 3);

            angle = (float)(eased * targetAngle);
            angle %= 360;
            pictureBoxWheel.Image = DrawWheelImage(angle);

            if (progress >= 1.0)
            {
                try { spinTimer.Stop(); } catch { }
                try { spinning = false; } catch { }
                try { spinSound?.Stop(); } catch { }
                try { winSound?.Play(); } catch { }

                string result = GetCurrentSelectedName();
                selectedNames.Add(result);

                MessageBox.Show(result, "축하드립니다!!");

                ProcessRouletteResult(result, selectedNames.Count);
                remainingNames.Remove(result);
                RedrawWheel();
            }
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
                    Brush brush = (i % 3 == 0) ? Brushes.Coral : (i % 3 == 1) ? Brushes.PaleGreen : Brushes.LightSkyBlue;
                    g.FillPie(brush, 0, 0, size, size, angleStart, sectionAngle);
                    g.DrawPie(Pens.White, 0, 0, size, size, angleStart, sectionAngle);

                    // 텍스트 배치
                    var midAngle = angleStart + sectionAngle / 2;
                    double rad = midAngle * Math.PI / 180;

                    // 텍스트 최대 길이(섹션 호 길이의 80%)
                    float centerX = size / 2f;
                    float centerY = size / 2f;
                    float outerRadius = size / 2f - 20; // 외곽선에서 20px 안쪽
                    float innerRadius = size / 4f + 10; // 너무 안쪽으로 들어가지 않게
                    float textRadius = (outerRadius + innerRadius) / 2f;

                    float arcLength = (float)(Math.PI * 2 * textRadius * (sectionAngle / 360.0));
                    float maxTextWidth = arcLength * 0.8f;

                    // 폰트 크기 자동 조정 (최대 12pt)
                    float minFont = 5f;
                    float maxFont = 12f;
                    float fontSize = maxFont;
                    string text = remainingNames[i];
                    SizeF textSize;

                    using (Font testFont = new Font(SystemFonts.DefaultFont.FontFamily, fontSize, FontStyle.Bold))
                    {
                        textSize = g.MeasureString(text, testFont);
                    }
                    // 폰트 크기 줄이기
                    while (textSize.Width > maxTextWidth && fontSize > minFont)
                    {
                        fontSize -= 0.5f;
                        using (Font testFont = new Font(SystemFonts.DefaultFont.FontFamily, fontSize, FontStyle.Bold))
                        {
                            textSize = g.MeasureString(text, testFont);
                        }
                    }

                    using (Font font = new Font(SystemFonts.DefaultFont.FontFamily, fontSize, FontStyle.Bold))
                    {
                        // 텍스트 중앙이 textRadius에 오도록
                        float x = (float)(centerX + Math.Cos(rad) * textRadius);
                        float y = (float)(centerY + Math.Sin(rad) * textRadius);

                        g.TranslateTransform(x, y);
                        g.RotateTransform((float)midAngle);

                        // 텍스트 중앙 정렬 (세로)
                        g.DrawString(text, font, Brushes.Black, -textSize.Width / 2, -textSize.Height / 2);

                        g.ResetTransform();
                    }

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

        private void MainForm_Resize(object sender, EventArgs e)
        {
            pictureBoxWheel.Image = DrawWheelImage(angle);
        }

        private void SaveMembersToCsv()
        {
            using (var sw = new StreamWriter("member.csv", false))
            {
                foreach (DataGridViewRow row in dgvMembers.Rows)
                {
                    if (row.IsNewRow) continue;
                    string name = row.Cells["mMemberColumn"].Value?.ToString() ?? "";
                    string result = row.Cells["mResultColumn"].Value?.ToString() ?? "";
                    sw.WriteLine($"{name},{result}");
                }
            }
        }

        private void SaveGiftsToCsv()
        {
            using (var sw = new StreamWriter("gift.csv", false))
            {
                foreach (DataGridViewRow row in dgvGifts.Rows)
                {
                    if (row.IsNewRow) continue;
                    string gift = row.Cells["gGiftColumn"].Value?.ToString() ?? "";
                    string member = row.Cells["gMemberColumn"].Value?.ToString() ?? "";
                    sw.WriteLine($"{gift},{member}");
                }
            }
        }

        private void ProcessRouletteResult(string selectedMember, int order)
        {
            // 멤버 결과 순서 기록
            for (int i = 0; i < dgvMembers.Rows.Count; i++)
            {
                if ((string)dgvMembers.Rows[i].Cells["mMemberColumn"].Value == selectedMember)
                {
                    dgvMembers.Rows[i].Cells["mResultColumn"].Value = order.ToString();
                    break;
                }
            }
            SaveMembersToCsv();

            // 선물-멤버 매칭 (아직 멤버가 없는 첫번째 선물에 할당)
            for (int i = 0; i < dgvGifts.Rows.Count; i++)
            {
                if (string.IsNullOrEmpty(dgvGifts.Rows[i].Cells["gMemberColumn"].Value?.ToString()))
                {
                    dgvGifts.Rows[i].Cells["gMemberColumn"].Value = selectedMember;
                    break;
                }
            }
            SaveGiftsToCsv();
        }

        private void TbSpinDuration_ValueChanged(object sender, EventArgs e)
        {
            spinDuration = (int)tbSpinDuration.Value * 1000 / spinTimer.Interval;
            lblSpinDuration.Text = $"{tbSpinDuration.Value}초";
        }
    }
}