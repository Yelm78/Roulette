using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Windows.Forms;
using System.Threading;

namespace Roulette
{
    public partial class MainForm : Form
    {
        private List<string> nameList = new List<string>();
        private List<string> remainingNames = new List<string>();
        private List<string> selectedNames = new List<string>();
        private Random random = new Random();
        private float angle = 0;
        private System.Windows.Forms.Timer spinTimer = new System.Windows.Forms.Timer();
        private SoundPlayer spinSound;
        private SoundPlayer winSound;

        // 물리 회전 관련 변수
        private bool spinning = false;
        private float accelTime = 0f, decelTime = 0f, softStopTime = 0f;
        private float totalAngle = 0f;
        private float elapsedTime = 0f;

        // 가속도와 감속도를 조절하는 변수
        private float accelerationFactor = 1.0f; // 가속도 조절 (값이 클수록 초반에 더 빨리 회전 시작)
        private float decelerationFactor = 2.0f; // 감속도 조절 (값이 클수록 마지막에 더 천천히 멈춤)
        private int resultIndex;
        private int spinDuration;
        private Stopwatch spinStopwatch = new Stopwatch();

        public MainForm()
        {
            InitializeComponent();

            // pbSpin 투명 설정
            pbSpin.BackColor = Color.Transparent;
            pbSpin.Parent = pbWheel;
            pbSpin.BringToFront();
            pbWheel.Resize += (s, e) => CenterSpinButton();

            spinTimer.Interval = 10;
            spinTimer.Tick += SpinTimer_Tick;

            try
            {
                spinSound = new SoundPlayer("Resources/spin.wav");
                winSound = new SoundPlayer("Resources/win.wav");
            }
            catch { }

            tbSpinDuration.ValueChanged += TbSpinDuration_ValueChanged;
            TbSpinDuration_ValueChanged(null, null);

            LoadCsvFiles();
            RedrawWheel();
        }

        private void TbSpinDuration_ValueChanged(object sender, EventArgs e)
        {
            spinDuration = (int)tbSpinDuration.Value * 1000 / spinTimer.Interval;
            lblSpinDuration.Text = $"{tbSpinDuration.Value}초";
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

        private void RedrawWheel()
        {
            angle = 0;
            pbWheel.Image = DrawWheelImage(angle);
            UpdatePbSpinBackColor();
            CenterSpinButton();
        }

        private Bitmap DrawWheelImage(float currentAngle)
        {
            int size = Math.Min(pbWheel.Width, pbWheel.Height) - 2;
            Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float sectionAngle = 360f / Math.Max(1, remainingNames.Count);
                float angleStart = currentAngle;

                List<Brush> pastelBrushes = new List<Brush>();
                for (int i = 0; i < remainingNames.Count; i++)
                {
                    pastelBrushes.Add(new SolidBrush(GetRandomPastelColor()));
                }

                for (int i = 0; i < remainingNames.Count; i++)
                {
                    Brush brush = pastelBrushes[i];
                    g.FillPie(brush, 0, 0, size, size, angleStart, sectionAngle);
                    g.DrawPie(Pens.White, 0, 0, size, size, angleStart, sectionAngle);

                    var midAngle = angleStart + sectionAngle / 2;
                    double rad = midAngle * Math.PI / 180;

                    float centerX = size / 2f;
                    float centerY = size / 2f;
                    float outerRadius = size / 2f - 20;
                    float innerRadius = size / 4f + 10;
                    float textRadius = (outerRadius + innerRadius) / 2f;

                    float arcLength = (float)(Math.PI * 2 * textRadius * (sectionAngle / 360.0));
                    float maxTextWidth = arcLength * 0.8f;

                    float minFont = 5f;
                    float maxFont = 12f;
                    float fontSize = maxFont;
                    string text = remainingNames[i];
                    SizeF textSize;

                    using (Font testFont = new Font(SystemFonts.DefaultFont.FontFamily, fontSize, FontStyle.Bold))
                    {
                        textSize = g.MeasureString(text, testFont);
                    }
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
                        float x = (float)(centerX + Math.Cos(rad) * textRadius);
                        float y = (float)(centerY + Math.Sin(rad) * textRadius);

                        g.TranslateTransform(x, y);
                        g.RotateTransform((float)midAngle);

                        g.DrawString(text, font, Brushes.Black, -textSize.Width / 2, -textSize.Height / 2);

                        g.ResetTransform();
                    }

                    angleStart += sectionAngle;
                }

                // 바늘
                g.FillPolygon(Brushes.DarkRed, new PointF[]
                {
                    new PointF(size / 2 - 10, 0),
                    new PointF(size / 2 + 10, 0),
                    new PointF(size / 2, 15)
                });

                // === 룰렛 가운데 투명 구멍 ===
                int holeSize = (int)(Math.Min(pbWheel.Width, pbWheel.Height) * 0.22);
                int holeX = (size - holeSize) / 2;
                int holeY = (size - holeSize) / 2;
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(holeX, holeY, holeSize, holeSize);
                    g.SetClip(path, CombineMode.Replace);
                    g.Clear(Color.Transparent);
                    g.ResetClip();
                }
            }
            return bmp;
        }

        private Color GetRandomPastelColor()
        {
            int r = random.Next(127, 256);
            int g = random.Next(127, 256);
            int b = random.Next(127, 256);

            r = (r + 255) / 2;
            g = (g + 255) / 2;
            b = (b + 255) / 2;
            return Color.FromArgb(r, g, b);
        }

        private void UpdatePbSpinBackColor()
        {
            if (pbWheel.Image == null) return;

            // pbSpin의 중앙이 pbWheel의 어디에 위치하는지 계산
            var spinCenter = pbSpin.PointToScreen(new Point(pbSpin.Width / 2, pbSpin.Height / 2));
            var wheelOrigin = pbWheel.PointToScreen(Point.Empty);

            int x = spinCenter.X - wheelOrigin.X;
            int y = spinCenter.Y - wheelOrigin.Y;

            // 이미지 크기와 pbWheel 컨트롤 크기가 다를 수 있으므로 비율 변환
            if (pbWheel.Image.Width != pbWheel.Width || pbWheel.Image.Height != pbWheel.Height)
            {
                x = x * pbWheel.Image.Width / pbWheel.Width;
                y = y * pbWheel.Image.Height / pbWheel.Height;
            }

            // 이미지 범위 내에 있는지 확인
            if (x >= 0 && y >= 0 && x < pbWheel.Image.Width && y < pbWheel.Image.Height)
            {
                Bitmap bmp = (Bitmap)pbWheel.Image;
                Color color = bmp.GetPixel(x, y);
                pbSpin.BackColor = color;
            }
        }

        private void CenterSpinButton()
        {
            // SPIN 버튼의 크기를 pbWheel 크기의 20%로 설정
            int size = (int)(Math.Min(pbWheel.Width, pbWheel.Height) * 0.2);
            size = Math.Max(size, 30); // 최소 크기
            pbSpin.Size = new Size(size, size);

            // pbSpin의 중심을 pbWheel 중심에 맞춤
            int centerX = pbWheel.Width / 2 - pbSpin.Width / 2;
            int centerY = pbWheel.Height / 2 - pbSpin.Height / 2;
            pbSpin.Location = new Point(centerX, centerY);
        }

        private void btnSpin_Click(object sender, EventArgs e)
        {
            if (spinning || remainingNames.Count == 0) return;
            spinning = true;
            spinStopwatch.Restart();

            float totalTime = (float)tbSpinDuration.Value;
            accelTime = 0f;
            decelTime = 0f;
            softStopTime = totalTime;

            float baseRotations = 6f * (random.Next(80, 301) * 0.01f);  // 짧은 시간일 때 기본 회전수(80% ~ 300% 사이의 랜덤 값)
            float boostFactor = 1.0f * (random.Next(100, 301) * 0.01f); // 시간에 따라 증가량(100% ~ 300% 사이의 랜덤 값)
            float timeThreshold = 10f * (random.Next(50, 201) * 0.01f); // 기준 시간 (10초 * 50% ~ 200% 사이의 랜덤 값)
            float timeRatio = Math.Clamp((totalTime - timeThreshold) / timeThreshold, 0f, 1f);
            float extraRotations = timeRatio * boostFactor * totalTime * (random.Next(80, 251) * 0.01f);  // 길수록 회전 더 많이(80% ~ 250% 사이의 랜덤 값)

            float rotations = baseRotations + extraRotations;
            totalAngle = 360f * rotations;

            angle = 0f;
            elapsedTime = 0f;

            try { spinTimer.Start(); } catch { }
            try { spinSound?.PlayLooping(); } catch { }
        }

        private void SpinTimer_Tick(object sender, EventArgs e)
        {
            float totalTime = (float)tbSpinDuration.Value;
            elapsedTime = spinStopwatch.ElapsedMilliseconds / 1000f;

            if (elapsedTime >= totalTime)
            {
                elapsedTime = totalTime;

                try { spinTimer.Stop(); } catch { }
                try { spinSound?.Stop(); } catch { }
                try { winSound?.Play(); } catch { }
                spinning = false;

                string result = GetCurrentSelectedName();
                selectedNames.Add(result);
                MessageBox.Show(result, "축하드립니다!!");
                ProcessRouletteResult(result, selectedNames.Count);
                remainingNames.Remove(result);
                pbWheel.Image = DrawWheelImage(angle);
                RedrawWheel();
                return;
            }

            float progress = elapsedTime / totalTime;

            // progress: 0(시작) ~ 1(끝)까지 진행률
            // accelerationFactor: 값이 크면 초반에 더 빠르게 가속
            float adjustedProgress = (float)Math.Pow(progress, accelerationFactor);

            // decelerationFactor: 값이 크면 마지막에 더 천천히 감속
            float eased = 1f - (float)Math.Pow(1f - adjustedProgress, decelerationFactor);

            // --- 잔진동 효과 (마지막에 바퀴가 살짝 흔들리는 효과) ---
            float dampingAmount = 5f;      // 흔들림의 크기(도 단위)
            float dampingWaves = 3f;       // 흔들림 횟수
            float damping = (1f - progress) * dampingAmount * (float)Math.Sin(progress * dampingWaves * Math.PI);

            // 최종 각도 계산 (eased로 부드럽게 회전, damping으로 잔진동 추가)
            angle = eased * totalAngle + damping;
            angle %= 360f;

            pbWheel.Image = DrawWheelImage(angle);
            UpdatePbSpinBackColor();
        }

        private string GetCurrentSelectedName()
        {
            if (remainingNames.Count == 0) return "";
            float sectionAngle = 360f / remainingNames.Count;
            float needleOffset = 270; // 선택 바늘이 12시 방향
            float normalizedAngle = (needleOffset - angle + 360) % 360;
            int index = (int)(normalizedAngle / sectionAngle);
            try { return remainingNames[index]; } catch { return ""; }
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

        private void SaveMembersToCsv()
        {
            Thread thread = new Thread(ThreadSaveMembersToCsv);
            thread.IsBackground = true;
            thread.Start();
        }

        private void ThreadSaveMembersToCsv()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate () { threadSaveMembersToCsv(); }));
                return;
            }
            threadSaveMembersToCsv();
        }

        private void threadSaveMembersToCsv()
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
            Thread thread = new Thread(ThreadSaveGiftsToCsv);
            thread.IsBackground = true;
            thread.Start();
        }

        private void ThreadSaveGiftsToCsv()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate () { threadSaveGiftsToCsv(); }));
                return;
            }
            threadSaveGiftsToCsv();
        }

        private void threadSaveGiftsToCsv()
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

        private void MainForm_Resize(object sender, EventArgs e)
        {
            pbWheel.Image = DrawWheelImage(angle);
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

        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Close();
            this.Dispose();
        }
    }
}