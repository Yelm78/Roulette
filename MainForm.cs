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
        // 멤버, 남은 멤버, 당첨된 멤버 리스트
        private List<string> nameList = new List<string>();
        private List<string> remainingNames = new List<string>();
        private List<string> selectedNames = new List<string>();

        // 랜덤 객체 (회전수, 색상 등 랜덤에 사용)
        private Random random = new Random();

        // 회전판 각도, 회전 관련 변수
        private float angle = 0;                // 현재 회전 각도
        private float totalAngle = 0f;          // 전체 회전할 각도(도)
        private float elapsedTime = 0f;         // 경과 시간(초)
        private float totalTime = 0f;           // 전체 회전 시간(초)

        // 회전 애니메이션 조절 변수
        private float accelerationFactor = 1.0f; // 가속도(초반 빠르게)
        private float decelerationFactor = 2.5f; // 감속도(마지막 천천히)

        // 회전 애니메이션 및 사운드
        private System.Windows.Forms.Timer spinTimer = new System.Windows.Forms.Timer();
        private Stopwatch spinStopwatch = new Stopwatch();
        private SoundPlayer SoundSpin;
        private SoundPlayer SoundResult;

        // 회전판 이미지 캐시(매번 새로 그리지 않고, 한 번만 그림)
        private Bitmap cachedWheelImage = null;

        // 회전 중 여부
        private bool spinning = false;

        public MainForm()
        {
            InitializeComponent();

            // 폼 및 컨트롤 폰트 고딕체로 변경
            this.Font = new Font("맑은 고딕", 9F, FontStyle.Regular);
            foreach (Control ctl in this.Controls)
                ctl.Font = new Font("맑은 고딕", ctl.Font.Size, ctl.Font.Style);

            // SPIN 버튼에 남은 시간 표시 이벤트 연결
            pbSpin.Paint += RemainSeconds;

            // SPIN 버튼을 회전판 위에 투명하게 올림
            pbSpin.BackColor = Color.Transparent;
            pbSpin.Parent = pbWheel;
            pbSpin.BringToFront();

            // 회전판 크기 변경 시 SPIN 버튼 위치 재조정
            pbWheel.Resize += (s, e) => CenterSpinButton();

            // 회전 타이머 설정 (10ms마다 Tick)
            spinTimer.Interval = 10;
            spinTimer.Tick += SpinTimer_Tick;

            // 사운드 파일 로드
            try { SoundSpin = new SoundPlayer("Resources/spin.wav"); } catch { }
            try { SoundResult = new SoundPlayer("Resources/result.wav"); } catch { }

            // 회전 시간 조절 트랙바 이벤트
            tbSpinDuration.ValueChanged += TbSpinDuration_ValueChanged;
            TbSpinDuration_ValueChanged(null, null);

            // 멤버/선물 파일 로드 및 회전판 그리기
            LoadCsvFiles();
            RedrawWheel();
        }

        // 회전 시간 트랙바 값 변경 시 라벨 갱신
        private void TbSpinDuration_ValueChanged(object sender, EventArgs e)
        {
            lblSpinDuration.Text = $"{tbSpinDuration.Value}초";
        }

        // 멤버/선물 CSV 파일 로드
        private void LoadCsvFiles()
        {
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
                            selectedNames.Add(name);
                        else
                            remainingNames.Add(name);
                    }
                }
            }

            // 선물 로드
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

        // 회전판 위에 바늘(삼각형) 그리기
        private void SelectionMarker(object sender, PaintEventArgs e)
        {
            int w = pbWheel.Width;
            int cx = w / 2;

            // 바늘 크기(비율로 조정)
            int needleWidth = Math.Max(w, pbWheel.Height) / 50;
            int needleHeight = Math.Max(w, pbWheel.Height) / 40;

            // 바늘 꼭짓점 좌표 (상단 중앙)
            PointF p1 = new PointF(cx - needleWidth / 2f, 0);
            PointF p2 = new PointF(cx + needleWidth / 2f, 0);
            PointF p3 = new PointF(cx, needleHeight);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Brush b = new SolidBrush(Color.DarkRed))
                e.Graphics.FillPolygon(b, new[] { p1, p2, p3 });
            using (Pen pen = new Pen(Color.White, 2))
                e.Graphics.DrawPolygon(pen, new[] { p1, p2, p3 });
        }

        // 회전판 이미지 새로 그리기 및 캐시
        private void RedrawWheel()
        {
            angle = 0;
            cachedWheelImage?.Dispose();
            cachedWheelImage = DrawWheelImage(0);
            pbWheel.Image = cachedWheelImage;
            UpdatePbSpinBackColor();
            CenterSpinButton();
        }

        // 고해상도 회전판 이미지 생성 (멤버가 바뀔 때만 호출)
        private Bitmap DrawWheelImage(float currentAngle)
        {
            // 바늘 Paint 이벤트 한 번만 연결
            pbWheel.Paint -= SelectionMarker;
            pbWheel.Paint += SelectionMarker;

            int scale = 5; // 고해상도 배수
            int size = (Math.Min(pbWheel.Width, pbWheel.Height) - 2) * scale;
            Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float sectionAngle = 360f / Math.Max(1, remainingNames.Count);
                float angleStart = currentAngle;

                // 파스텔 색상 리스트 생성
                List<Brush> pastelBrushes = new List<Brush>();
                for (int i = 0; i < remainingNames.Count; i++)
                { pastelBrushes.Add(new SolidBrush(GetRandomPastelColor())); }

                // 각 멤버별로 섹션 그리기
                for (int i = 0; i < remainingNames.Count; i++)
                {
                    Brush brush = pastelBrushes[i];
                    g.FillPie(brush, 0, 0, size, size, angleStart, sectionAngle);
                    g.DrawPie(Pens.White, 0, 0, size, size, angleStart, sectionAngle);

                    // 섹션 중앙 각도
                    var midAngle = angleStart + sectionAngle / 2;
                    double rad = midAngle * Math.PI / 180;

                    float centerX = size / 2f;
                    float centerY = size / 2f;
                    float outerRadius = size / 2f - 20 * scale;
                    float innerRadius = size / 4f + 10 * scale;
                    float textRadius = (outerRadius + innerRadius) / 2f;

                    float arcLength = (float)(Math.PI * 2 * textRadius * (sectionAngle / 360.0));
                    float maxTextWidth = arcLength * 0.8f;

                    float minFont = 5f * scale;
                    float maxFont = 12f * scale;
                    float fontSize = maxFont;
                    string text = remainingNames[i];
                    SizeF textSize;

                    // 폰트 크기 자동 조정 (섹션에 맞게)
                    using (Font testFont = new Font("맑은 고딕", fontSize, FontStyle.Bold))
                    { textSize = g.MeasureString(text, testFont); }
                    while (textSize.Width > maxTextWidth && fontSize > minFont)
                    {
                        fontSize -= 0.5f * scale;
                        using (Font testFont = new Font("맑은 고딕", fontSize, FontStyle.Bold))
                            textSize = g.MeasureString(text, testFont);
                    }

                    // 섹션 중앙에 이름 그리기
                    using (Font font = new Font("맑은 고딕", fontSize, FontStyle.Bold))
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

                // 가운데 투명 구멍
                int holeSize = (int)(Math.Min(pbWheel.Width, pbWheel.Height) * 0.2 * scale);
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

            // 실제 표시 크기로 다운샘플링
            Bitmap resized = new Bitmap(bmp, Math.Min(pbWheel.Width, pbWheel.Height) - scale, Math.Min(pbWheel.Width, pbWheel.Height) - scale);
            bmp.Dispose();
            return resized;
        }

        // 파스텔톤 랜덤 색상 생성
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

        // SPIN 버튼 배경색을 회전판과 맞춤
        private void UpdatePbSpinBackColor()
        {
            if (pbWheel.Image == null) return;

            // SPIN 버튼 중앙 좌표 계산
            var spinCenter = pbSpin.PointToScreen(new Point(pbSpin.Width / 2, pbSpin.Height / 2));
            var wheelOrigin = pbWheel.PointToScreen(Point.Empty);

            int x = spinCenter.X - wheelOrigin.X;
            int y = spinCenter.Y - wheelOrigin.Y;

            // 이미지 크기와 컨트롤 크기가 다를 때 비율 변환
            if (pbWheel.Image.Width != pbWheel.Width || pbWheel.Image.Height != pbWheel.Height)
            {
                x = x * pbWheel.Image.Width / pbWheel.Width;
                y = y * pbWheel.Image.Height / pbWheel.Height;
            }

            // 이미지 범위 내에 있으면 색상 추출
            if (x >= 0 && y >= 0 && x < pbWheel.Image.Width && y < pbWheel.Image.Height)
            {
                Bitmap bmp = (Bitmap)pbWheel.Image;
                Color color = bmp.GetPixel(x, y);
                pbSpin.BackColor = color;
            }
        }

        // SPIN 버튼을 회전판 중앙에 위치
        private void CenterSpinButton()
        {
            int size = (int)(Math.Min(pbWheel.Width, pbWheel.Height) * 0.2);
            size = Math.Max(size, 30);
            pbSpin.Size = new Size(size, size);

            int centerX = pbWheel.Width / 2 - pbSpin.Width / 2;
            int centerY = pbWheel.Height / 2 - pbSpin.Height / 2;
            pbSpin.Location = new Point(centerX, centerY);
        }

        // SPIN 버튼 클릭 시 회전 시작
        private void btnSpin_Click(object sender, EventArgs e)
        {
            pbSpin.BackgroundImage = Properties.Resources.SPIN3;

            if (spinning || remainingNames.Count == 0) return;
            spinning = true;
            spinStopwatch.Restart();

            // 회전 시간(초) + 0 ~ 9.9초 랜덤 추가
            totalTime = (float)tbSpinDuration.Value + (random.Next(0, 100) * 0.1f);

            // 회전수 계산 (랜덤성 부여)
            float baseRotations = (totalTime * 0.6f) * (random.Next(80, 301) * 0.01f);
            float boostFactor = 1.0f * (random.Next(100, 301) * 0.01f);
            float timeThreshold = 10f * (random.Next(50, 201) * 0.01f);
            float timeRatio = Math.Clamp((totalTime - timeThreshold) / timeThreshold, 0f, 1f);
            float extraRotations = timeRatio * boostFactor * totalTime * (random.Next(80, 251) * 0.01f);

            float rotations = baseRotations + extraRotations;
            totalAngle = 360f * rotations;

            angle = 0f;
            elapsedTime = 0f;

            try { spinTimer.Start(); } catch { }
            try { SoundSpin?.PlayLooping(); } catch { }
        }

        // 회전 애니메이션 처리 (쓰레드로 비동기 처리)
        private void SpinTimer_Tick(object sender, EventArgs e)
        {
            Thread thread = new Thread(ThreadSpinTimer_Tick);
            thread.IsBackground = true;
            thread.Start();
        }
        private void ThreadSpinTimer_Tick()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate () { threadSpinTimer_Tick(); }));
                return;
            }
            threadSpinTimer_Tick();
        }
        private void threadSpinTimer_Tick()
        {
            elapsedTime = spinStopwatch.ElapsedMilliseconds / 1000f;

            if (elapsedTime >= totalTime)
            {
                elapsedTime = totalTime;

                try { spinTimer.Stop(); } catch { }
                try { SoundSpin?.Stop(); } catch { }
                try { SoundResult?.Play(); } catch { }
                spinning = false;

                string result = GetCurrentSelectedName();
                selectedNames.Add(result);
                MessageBox.Show(result, "축하드립니다!!");
                ProcessRouletteResult(result, selectedNames.Count);
                remainingNames.Remove(result);
                pbWheel.Image = DrawWheelImage(angle);
                RedrawWheel();
                pbSpin.BackgroundImage = Properties.Resources.SPIN;
                pbSpin.Invalidate(); // 남은 시간 표시 갱신
                return;
            }

            float progress = elapsedTime / totalTime;

            // 가속/감속 곡선 적용
            float adjustedProgress = (float)Math.Pow(progress, accelerationFactor);
            float eased = 1f - (float)Math.Pow(1f - adjustedProgress, decelerationFactor);

            // 잔진동 효과
            float dampingAmount = 5f;
            float dampingWaves = 3f;
            float damping = (1f - progress) * dampingAmount * (float)Math.Sin(progress * dampingWaves * Math.PI);

            // 최종 각도 계산
            angle = eased * totalAngle + damping;
            angle %= 360f;

            // 캐시된 이미지를 회전만 시킴 (성능 최적화)
            if (cachedWheelImage != null)
                pbWheel.Image = RotateImage(cachedWheelImage, angle);

            UpdatePbSpinBackColor();
            pbSpin.Invalidate(); // 남은 시간 표시 갱신
        }

        // 이미지를 지정 각도만큼 회전
        private Bitmap RotateImage(Bitmap src, float angle)
        {
            if (src == null) return null;
            Bitmap dst = new Bitmap(src.Width, src.Height);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.TranslateTransform(src.Width / 2f, src.Height / 2f);
                g.RotateTransform(angle);
                g.TranslateTransform(-src.Width / 2f, -src.Height / 2f);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0);
            }
            return dst;
        }

        // SPIN 버튼에 남은 시간(초) 표시
        private void RemainSeconds(object sender, PaintEventArgs e)
        {
            if (spinning)
            {
                float remain = Math.Max(0, totalTime - elapsedTime);
                string text = remain.ToString("000"); // 3자리

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font font = new Font("맑은 고딕", pbSpin.Height / 4f, FontStyle.Bold))
                using (Brush brush = new SolidBrush(Color.Black))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(text, font, brush, pbSpin.ClientRectangle, sf);
                }
            }
        }

        // 현재 바늘이 가리키는 멤버 이름 반환
        private string GetCurrentSelectedName()
        {
            if (remainingNames.Count == 0) return "";
            float sectionAngle = 360f / remainingNames.Count;
            float needleOffset = 270; // 바늘이 12시 방향
            float normalizedAngle = (needleOffset - angle + 360) % 360;
            int index = (int)(normalizedAngle / sectionAngle);
            try { return remainingNames[index]; } catch { return ""; }
        }

        // 당첨 결과 처리 (멤버/선물 매칭 및 저장)
        private void ProcessRouletteResult(string selectedMember, int order)
        {
            // 멤버 결과 기록
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

        // 멤버 CSV 저장 (쓰레드로 비동기 처리)
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

        // 선물 CSV 저장 (쓰레드로 비동기 처리)
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

        // 폼 크기 변경 시 회전판/버튼 재배치
        private void MainForm_Resize(object sender, EventArgs e)
        {
            RedrawWheel();
        }

        // 멤버 추가 버튼 클릭
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

        // 선물 추가 버튼 클릭
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

        // SPIN 버튼 마우스 오버/리브 시 이미지 변경
        private void pbSpin_MouseHover(object sender, EventArgs e)
        {
            if (!spinning)
                pbSpin.BackgroundImage = Properties.Resources.SPIN2;
        }
        private void pbSpin_MouseLeave(object sender, EventArgs e)
        {
            if (!spinning)
                pbSpin.BackgroundImage = Properties.Resources.SPIN;
        }

        // 프로그램 종료 버튼 클릭
        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Close();
            this.Dispose();
        }
    }
}