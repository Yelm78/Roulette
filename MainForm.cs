using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Roulette
{
    // 이 클래스는 룰렛 프로그램의 메인 폼(윈도우)입니다.
    public partial class MainForm : Form
    {
        // CSV 파일을 동시에 여러 곳에서 접근하지 못하게 잠금용 객체를 만듭니다.
        private static readonly object memberCsvLock = new object();
        private static readonly object giftCsvLock = new object();

        // 전체 멤버 목록, 아직 당첨되지 않은 멤버, 이미 당첨된 멤버를 저장하는 리스트입니다.
        private List<string> nameList = new List<string>();
        private List<string> remainingNames = new List<string>();
        private List<string> selectedNames = new List<string>();

        // 랜덤값을 만들 때 사용하는 객체입니다.
        private Random random = new Random();

        // 회전판의 각도와 시간 관련 변수들입니다.
        private float angle = 0;                // 현재 회전 각도
        private float totalAngle = 0f;          // 한 번 돌릴 때 전체 회전 각도(도)
        private float elapsedTime = 0f;         // 회전 시작 후 지난 시간(초)
        private float totalTime = 0f;           // 한 번 돌릴 때 걸리는 전체 시간(초)

        // 회전 애니메이션의 가속/감속 정도를 조절하는 변수입니다.
        private float accelerationFactor = 1.0f; // 초반에 빠르게
        private float decelerationFactor = 2.5f; // 마지막에 천천히

        // 회전 애니메이션과 사운드 재생에 사용하는 객체들입니다.
        private System.Windows.Forms.Timer spinTimer = new System.Windows.Forms.Timer();
        private Stopwatch spinStopwatch = new Stopwatch();
        private SoundPlayer SoundSpin;
        private SoundPlayer SoundResult;

        // 회전판 이미지를 캐싱해서 성능을 높입니다.
        private Bitmap cachedWheelImage = null;
        private Image btnSpinImage;
        private Image btnSpin2Image;
        private Image btnSpin3Image;

        // 사운드 파일을 메모리에서 직접 읽어오기 위한 스트림입니다.
        private MemoryStream soundSpinStream;
        private MemoryStream soundResultStream;

        // 현재 회전 중인지 여부를 저장합니다.
        private bool spinning = false;

        // 당첨자 이름을 저장합니다.
        private string winnerName = null;

        // 폼이 생성될 때 실행되는 코드입니다.
        public MainForm()
        {
            InitializeComponent();

            // 폼과 모든 컨트롤의 폰트를 '맑은 고딕'으로 통일합니다.
            this.Font = new Font("맑은 고딕", 9F, FontStyle.Regular);
            foreach (Control ctl in this.Controls)
                ctl.Font = new Font("맑은 고딕", ctl.Font.Size, ctl.Font.Style);

            // SPIN 버튼 이미지를 불러오고, 기본 이미지를 설정합니다.
            try
            {
                btnSpinImage = Image.FromStream(new MemoryStream(Properties.Resources.btnSPIN));
                btnSpin2Image = Image.FromStream(new MemoryStream(Properties.Resources.btnSPIN2));
                btnSpin3Image = Image.FromStream(new MemoryStream(Properties.Resources.btnSPIN3));
                pbSpin.BackgroundImage = btnSpinImage;
            }
            catch (Exception ex)
            { LogError(ex.ToString()); }

            // SPIN 버튼에 남은 시간/당첨자 표시를 위한 Paint 이벤트를 연결합니다.
            pbSpin.Paint += RemainSeconds;

            // SPIN 버튼을 회전판 위에 올리고, 투명하게 만듭니다.
            pbSpin.BackColor = Color.Transparent;
            pbSpin.Parent = pbWheel;
            pbSpin.BringToFront();

            // 회전판 크기가 바뀌면 SPIN 버튼 위치를 다시 맞춥니다.
            pbWheel.Resize += (s, e) => CenterSpinButton();

            // 회전 타이머를 10ms마다 실행되도록 설정합니다.
            spinTimer.Interval = 10;
            spinTimer.Tick += SpinTimer_Tick;

            // 사운드 파일을 메모리에서 불러옵니다.
            try
            {
                soundSpinStream = new MemoryStream(Properties.Resources.SoundSpin);
                SoundSpin = new SoundPlayer(soundSpinStream);
                SoundSpin.Load();
            }
            catch (Exception ex) { LogError(ex.ToString()); }
            try
            {
                soundResultStream = new MemoryStream(Properties.Resources.SoundResult);
                SoundResult = new SoundPlayer(soundResultStream);
                SoundResult.Load();
            }
            catch (Exception ex) { LogError(ex.ToString()); }

            // 회전 시간 조절 트랙바 이벤트 연결 및 초기화
            tbSpinDuration.ValueChanged += TbSpinDuration_ValueChanged;
            TbSpinDuration_ValueChanged(null, null);

            // 멤버/선물 파일을 비동기로 불러옵니다.
            _ = LoadCsvFilesAsync();

            // 회전판 이미지를 그립니다.
            RedrawWheel();
        }

        // 트랙바 값이 바뀔 때마다 라벨에 시간을 표시합니다.
        private void TbSpinDuration_ValueChanged(object sender, EventArgs e)
        {
            lblSpinDuration.Text = $"{tbSpinDuration.Value}초";
        }

        // 멤버/선물 CSV 파일을 비동기로 읽어와서 리스트와 그리드뷰를 갱신합니다.
        private async Task LoadCsvFilesAsync()
        {
            string[] memberLines;
            string[] giftLines;

            try
            {
                memberLines = File.Exists("member.csv") ? await Task.Run(() => File.ReadAllLines("member.csv")) : Array.Empty<string>();
                giftLines = File.Exists("gift.csv") ? await Task.Run(() => File.ReadAllLines("gift.csv")) : Array.Empty<string>();
            }
            catch (Exception ex)
            {
                MessageBox.Show("CSV 파일을 읽는 중 오류가 발생했습니다.\nCSV 파일을 닫고 다시 실행하세요.:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                memberLines = Array.Empty<string>();
                giftLines = Array.Empty<string>();
                LogError(ex.ToString());
            }

            // UI 업데이트는 반드시 UI 스레드에서
            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            await Task.Factory.StartNew(() =>
            {
                nameList.Clear();
                remainingNames.Clear();
                selectedNames.Clear();
                dgvMembers.Rows.Clear();
                dgvGifts.Rows.Clear();

                foreach (var line in memberLines)
                {
                    var parts = line.Split(',');
                    string name = parts[0].Trim();
                    name = name.Replace("\"", ""); // 따옴표 제거
                    string result = parts.Length > 1 ? parts[1].Trim() : "";
                    if (!string.IsNullOrEmpty(name) && !nameList.Contains(name))
                    {
                        int rowIndex = dgvMembers.Rows.Add();
                        dgvMembers.Rows[rowIndex].Cells["mMemberColumn"].Value = name;
                        dgvMembers.Rows[rowIndex].Cells["mResultColumn"].Value = result;
                        nameList.Add(name);

                        if (!string.IsNullOrEmpty(result))
                            selectedNames.Add(name);
                        else
                            remainingNames.Add(name);
                    }
                }

                foreach (var line in giftLines)
                {
                    var parts = line.Split(',');
                    string gift = parts[0].Trim();
                    gift = gift.Replace("\"", ""); // 따옴표 제거
                    string member = parts.Length > 1 ? parts[1].Trim() : "";
                    if (!string.IsNullOrEmpty(gift))
                    {
                        int rowIndex = dgvGifts.Rows.Add();
                        dgvGifts.Rows[rowIndex].Cells["gGiftColumn"].Value = gift;
                        dgvGifts.Rows[rowIndex].Cells["gMemberColumn"].Value = member;
                    }
                }
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());

            RedrawWheel();
        }

        // 회전판 위에 바늘(삼각형)을 그립니다.
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
            using (Pen pen = new Pen(SystemColors.Control, 2))
                e.Graphics.DrawPolygon(pen, new[] { p1, p2, p3 });
        }

        // 회전판 이미지를 새로 그리고, 캐시에 저장합니다.
        private void RedrawWheel()
        {
            angle = 0;
            if (pbWheel.Image != null && pbWheel.Image != cachedWheelImage)
                pbWheel.Image.Dispose();
            cachedWheelImage?.Dispose();
            try
            {
                cachedWheelImage = DrawWheelImage(angle);
                pbWheel.Image = cachedWheelImage ?? throw new Exception("회전판 이미지 생성 실패");
            }
            catch (Exception ex)
            {
                pbWheel.Image = null;
                MessageBox.Show("회전판 이미지를 생성할 수 없습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);

                LogError(ex.ToString());
            }
            UpdatePbSpinBackColor();
            CenterSpinButton();
        }

        // 고해상도 회전판 이미지를 만듭니다.
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

        // 파스텔톤 랜덤 색상을 만듭니다.
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

        // SPIN 버튼의 배경색을 회전판과 맞춥니다.
        private void UpdatePbSpinBackColor()
        {
            if (pbSpin.IsDisposed || !pbSpin.IsHandleCreated) return;
            if (pbWheel.Image is not Bitmap bmp) return;

            // SPIN 버튼 중앙 좌표 계산
            if (!pbSpin.IsDisposed && pbSpin.IsHandleCreated)
            {
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
                if (x >= 0 && y >= 0 && x < bmp.Width && y < bmp.Height)
                {
                    Color color = bmp.GetPixel(x, y);
                    pbSpin.BackColor = color;
                }
            }
        }

        // SPIN 버튼을 회전판 중앙에 위치시킵니다.
        private void CenterSpinButton()
        {
            int size = (int)(Math.Min(pbWheel.Width, pbWheel.Height) * 0.25);
            size = Math.Max(size, 30);
            pbSpin.Size = new Size(size, size);

            int centerX = pbWheel.Width / 2 - pbSpin.Width / 2;
            int centerY = pbWheel.Height / 2 - pbSpin.Height / 2;
            pbSpin.Location = new Point(centerX, centerY);
        }

        // SPIN 버튼을 클릭하면 회전을 시작합니다.
        private void btnSpin_Click(object sender, EventArgs e)
        {
            try { soundSpinStream.Position = 0; SoundSpin?.PlayLooping(); } catch (Exception ex) { LogError(ex.ToString()); }

            pbWheel.Image = DrawWheelImage(angle);
            RedrawWheel();
            //pbSpin.BackgroundImage = btnSpinImage;
            try { pbSpin.Invalidate(); } catch (Exception ex) { LogError(ex.ToString()); }

            pbSpin.BackgroundImage = btnSpin3Image;
            winnerName = null;

            if (spinning || remainingNames.Count == 0) return;
            spinning = true;
            spinStopwatch.Restart();

            // 회전 시간(초) + 1 ~ 9.9초 랜덤 추가
            totalTime = (float)tbSpinDuration.Value + (random.Next(1, 100) * 0.1f);

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

            try { spinTimer.Start(); } catch (Exception ex) { LogError(ex.ToString()); }
        }

        // 타이머 Tick마다 회전 애니메이션을 처리합니다.
        private void SpinTimer_Tick(object sender, EventArgs e)
        {
            ThreadSpinTimer_Tick();
        }
        private void ThreadSpinTimer_Tick()
        {
            if (InvokeRequired)
            {
                // async/await로 대체
                _ = Task.Factory.StartNew(() => threadSpinTimer_Tick(), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
                return;
            }
            threadSpinTimer_Tick();
        }
        private void threadSpinTimer_Tick()
        {
            try
            {
                elapsedTime = spinStopwatch.ElapsedMilliseconds / 1000f;
            }
            catch (Exception ex)
            {
                // 예외 발생 시 프로그램 종료 방지
                MessageBox.Show("회전 중 오류(1)):\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                try { spinTimer.Stop(); } catch (Exception e) { LogError(e.ToString()); }
                spinning = false;

                LogError(ex.ToString());
            }

            // 회전이 끝났을 때 처리
            if (elapsedTime >= totalTime)
            {
                elapsedTime = totalTime;

                try { spinTimer.Stop(); } catch (Exception ex) { LogError(ex.ToString()); }
                try { SoundSpin?.Stop(); } catch (Exception ex) { LogError(ex.ToString()); }
                try { soundResultStream.Position = 0; SoundResult?.Play(); } catch (Exception ex) { LogError(ex.ToString()); }
                spinning = false;
                try
                {
                    string result = GetCurrentSelectedName();
                    selectedNames.Add(result);
                    ProcessRouletteResult(result, selectedNames.Count);
                    remainingNames.Remove(result);

                    winnerName = "Win!\n" + result;
                    pbSpin.Invalidate(); // 당첨자 이름 표시 갱신
                                         //MessageBox.Show(result, "축하드립니다!!");                    
                }
                catch (Exception ex)
                {
                    // 예외 발생 시 프로그램 종료 방지
                    MessageBox.Show("회전 중 오류(2)):\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    try { spinTimer.Stop(); } catch (Exception e) { LogError(e.ToString()); }
                    spinning = false;

                    LogError(ex.ToString());
                }
                return;
            }

            try
            {
                float progress = elapsedTime / totalTime;

                // 가속/감속 곡선 적용
                float adjustedProgress = (float)Math.Pow(progress, accelerationFactor);
                float eased = 1f - (float)Math.Pow(1f - adjustedProgress, decelerationFactor);

                // 최종 각도 계산
                angle = eased * totalAngle;
                angle %= 360f;

                // 캐시된 이미지를 회전만 시킴 (성능 최적화)
                if (cachedWheelImage != null)
                {
                    var oldImage = pbWheel.Image;
                    var rotated = RotateImage(cachedWheelImage, angle);
                    pbWheel.Image = rotated;
                    if (oldImage != null && oldImage != cachedWheelImage)
                        oldImage.Dispose();
                }
            }
            catch (Exception ex)
            {
                // 예외 발생 시 프로그램 종료 방지
                MessageBox.Show("회전 중 오류(3)):\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                try { spinTimer.Stop(); } catch (Exception e) { LogError(e.ToString()); }
                spinning = false;

                LogError(ex.ToString());
            }
            UpdatePbSpinBackColor();
            pbSpin.Invalidate(); // 남은 시간 표시 갱신
        }

        // 이미지를 지정한 각도만큼 회전시켜 새 Bitmap을 만듭니다.
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

        // SPIN 버튼에 남은 시간(초) 또는 당첨자 이름을 표시합니다.
        private void RemainSeconds(object sender, PaintEventArgs e)
        {
            try
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (spinning)
                {
                    float remain = Math.Max(0, totalTime - elapsedTime);
                    string text = ((int)remain).ToString("D3"); // 남은 시간을 3자리 숫자로 표시

                    float fontSize = Math.Max(3, pbSpin.Height / 4f);

                    // 남은 시간에 따라 텍스트 색상을 바꾼다
                    // - 3초 이상: 검정에서 빨강으로 점점 변함
                    // - 3초 미만: 완전히 빨강
                    // - 0초(000): 흰색
                    Color textColor;
                    if (remain < 1.0f)
                    {
                        // 시간이 0초일 때는 흰색
                        textColor = Color.White;
                    }
                    else if (remain < 3f)
                    {
                        // 3초 미만이면 빨강
                        textColor = Color.Red;
                    }
                    else
                    {
                        // 3초 이상이면 검정에서 빨강으로 점점 변함
                        // t가 0이면 검정, t가 1이면 빨강
                        float t = Math.Clamp((totalTime - remain) / (totalTime - 3f), 0f, 1f);
                        int r = (int)(255 * t); // t에 따라 빨강(R) 값 증가
                        textColor = Color.FromArgb(r, 0, 0);
                    }

                    using (Font font = new Font("맑은 고딕", fontSize, FontStyle.Bold))
                    using (Brush brush = new SolidBrush(textColor))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(text, font, brush, pbSpin.ClientRectangle, sf);
                    }
                }
                else if (!string.IsNullOrEmpty(winnerName))
                {
                    // 당첨자 이름을 파란색으로 중앙에 표시 (이름이 길면 폰트 크기를 자동 조정)
                    float maxFontSize = pbSpin.Height / 4f;
                    float minFontSize = 8f;
                    float fontSize = maxFontSize;
                    SizeF textSize;

                    using (Graphics gTest = pbSpin.CreateGraphics())
                    {
                        using (Font testFont = new Font("맑은 고딕", fontSize, FontStyle.Bold))
                        {
                            textSize = gTest.MeasureString(winnerName, testFont);
                        }
                        // 텍스트가 버튼 너비를 넘지 않도록 폰트 크기를 줄임
                        while (textSize.Width > pbSpin.Width * 0.9f && fontSize > minFontSize)
                        {
                            fontSize -= 1f;
                            using (Font testFont = new Font("맑은 고딕", fontSize, FontStyle.Bold))
                            {
                                textSize = gTest.MeasureString(winnerName, testFont);
                            }
                        }
                    }

                    using (Font font = new Font("맑은 고딕", fontSize, FontStyle.Bold))
                    using (Brush brush = new SolidBrush(Color.Blue))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(winnerName, font, brush, pbSpin.ClientRectangle, sf);
                    }
                }
                else
                {
                    // 회전 전에는 남은 선물 확률을 표시
                    int memberCount = remainingNames.Count;
                    int giftCount = 0;
                    foreach (DataGridViewRow row in dgvGifts.Rows)
                    {
                        if (row.IsNewRow) continue;
                        if (string.IsNullOrEmpty(row.Cells["gMemberColumn"].Value?.ToString()))
                            giftCount++;
                    }
                    double probability = (memberCount > 0) ? (giftCount * 100.0 / memberCount) : 0.0;
                    string text = $"\n\n{probability:0.#}%";

                    float fontSize = Math.Max(8, pbSpin.Height / 7f);
                    using (Font font = new Font("맑은 고딕", fontSize, FontStyle.Bold))
                    using (Brush brush = new SolidBrush(Color.Gray))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(text, font, brush, pbSpin.ClientRectangle, sf);
                    }
                }
            }
            catch (Exception ex)
            {
                try { File.AppendAllText("error.log", $"{DateTime.Now}: RemainSeconds: {ex}\n"); } catch { }
            }
        }

        // 현재 바늘이 가리키는 멤버 이름을 반환합니다.
        private string GetCurrentSelectedName()
        {
            if (remainingNames.Count == 0) return "";
            float sectionAngle = 360f / remainingNames.Count;
            float needleOffset = 270; // 바늘이 12시 방향
            float normalizedAngle = (needleOffset - angle + 360) % 360;
            int index = (int)(normalizedAngle / sectionAngle);
            try { return remainingNames[index]; }
            catch (Exception ex)
            {
                LogError(ex.ToString());
                return "";
            }
        }

        // 당첨 결과를 처리하고, 멤버/선물 정보를 저장합니다.
        private async void ProcessRouletteResult(string selectedMember, int order)
        {
            // 멤버 결과 기록 및 선택
            for (int i = 0; i < dgvMembers.Rows.Count; i++)
            {
                if ((string)dgvMembers.Rows[i].Cells["mMemberColumn"].Value == selectedMember)
                {
                    dgvMembers.Rows[i].Cells["mResultColumn"].Value = order.ToString();
                    dgvMembers.ClearSelection(); // 기존 선택 해제
                    dgvMembers.Rows[i].Selected = true; // 해당 멤버 행 선택
                    dgvMembers.CurrentCell = dgvMembers.Rows[i].Cells["mMemberColumn"]; // 포커스 이동(선택 강조)
                    break;
                }
            }
            await SaveMembersToCsv();

            // 선물-멤버 매칭 및 선택
            for (int i = 0; i < dgvGifts.Rows.Count; i++)
            {
                if (string.IsNullOrEmpty(dgvGifts.Rows[i].Cells["gMemberColumn"].Value?.ToString()))
                {
                    dgvGifts.Rows[i].Cells["gMemberColumn"].Value = selectedMember;
                    dgvGifts.ClearSelection(); // 기존 선택 해제
                    dgvGifts.Rows[i].Selected = true; // 해당 선물 행 선택
                    dgvGifts.CurrentCell = dgvGifts.Rows[i].Cells["gGiftColumn"]; // 포커스 이동(선택 강조)
                    break;
                }
            }
            await SaveGiftsToCsv();
        }

        // 멤버 정보를 CSV 파일로 저장합니다.
        private async Task SaveMembersToCsv()
        {
            var lines = new List<string>();
            foreach (DataGridViewRow row in dgvMembers.Rows)
            {
                if (row.IsNewRow) continue;
                string name = row.Cells["mMemberColumn"].Value?.ToString() ?? "";
                string result = row.Cells["mResultColumn"].Value?.ToString() ?? "";
                lines.Add($"{name},{result}");
            }

            try
            {
                await Task.Run(() =>
                {
                    lock (memberCsvLock)
                    {
                        File.WriteAllLines("member.csv", lines);
                    }
                });
            }
            catch (Exception ex)
            {
                if (SynchronizationContext.Current == null)
                    SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                await Task.Factory.StartNew(() =>
                {
                    MessageBox.Show("CSV 파일을 쓰는 중 오류가 발생했습니다.\n실행중에는 CSV 파일을 열지 마세요.:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());

                LogError(ex.ToString());
            }
        }

        // 선물 정보를 CSV 파일로 저장합니다.
        private async Task SaveGiftsToCsv()
        {
            var lines = new List<string>();
            foreach (DataGridViewRow row in dgvGifts.Rows)
            {
                if (row.IsNewRow) continue;
                string gift = row.Cells["gGiftColumn"].Value?.ToString() ?? "";
                string member = row.Cells["gMemberColumn"].Value?.ToString() ?? "";
                lines.Add($"{gift},{member}");
            }

            try
            {
                await Task.Run(() =>
                {
                    lock (giftCsvLock)
                    {
                        File.WriteAllLines("gift.csv", lines);
                    }
                });
            }
            catch (Exception ex)
            {
                if (SynchronizationContext.Current == null)
                    SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                await Task.Factory.StartNew(() =>
                {
                    MessageBox.Show("CSV 파일을 쓰는 중 오류가 발생했습니다.\n실행중에는 CSV 파일을 열지 마세요.:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());

                LogError(ex.ToString());
            }
        }

        // 폼 크기가 바뀌면 회전판과 버튼을 다시 배치합니다.
        private void MainForm_Resize(object sender, EventArgs e)
        {
            RedrawWheel();
        }

        // 멤버 추가 버튼 클릭 시 멤버를 추가합니다.
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

        // 선물 추가 버튼 클릭 시 선물을 추가합니다.
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

        // SPIN 버튼에 마우스를 올리면 이미지 변경
        private void pbSpin_MouseHover(object sender, EventArgs e)
        {
            if (!spinning)
            {
                pbSpin.BackgroundImage = btnSpin2Image;
                winnerName = null;
            }
        }
        // SPIN 버튼에서 마우스를 떼면 이미지 원래대로
        private void pbSpin_MouseLeave(object sender, EventArgs e)
        {
            if (!spinning)
            {
                pbSpin.BackgroundImage = btnSpinImage;
                winnerName = null;
            }
        }

        // 에러 로그를 파일로 남깁니다.
        private void LogError(string message)
        {
            try
            {
                File.AppendAllText("error.log", $"\n\n{DateTime.Now}: {message}\n");
            }
            catch (Exception ex) { LogError(ex.ToString()); }
        }

        // 종료 버튼 클릭 시 프로그램을 닫습니다.
        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Close();
            this.Dispose();
        }
    }
}