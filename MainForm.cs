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
using System.Xml.Linq;

namespace Roulette
{
    // 룰렛 프로그램의 메인 폼 클래스입니다.
    public partial class Roulette : Form
    {
        // CSV 파일 동시 접근 방지용 객체
        private static readonly object memberCsvLock = new object();
        private static readonly object giftCsvLock = new object();

        // 멤버, 남은 멤버, 당첨 멤버 리스트
        private List<string> nameList = new List<string>();
        private List<string> remainingNames = new List<string>();
        private List<string> selectedNames = new List<string>();

        // 랜덤 객체
        private Random random = new Random();

        // 회전판 각도 및 시간 관련 변수
        private float angle = 0;
        private float totalAngle = 0f;
        private float elapsedTime = 0f;
        private float totalTime = 0f;

        // 가속/감속 조절 변수
        private float accelerationFactor = 1.0f;
        private float decelerationFactor = 2.5f;

        // 회전 애니메이션, 사운드 관련 객체
        private System.Windows.Forms.Timer spinTimer = new System.Windows.Forms.Timer();
        private Stopwatch spinStopwatch = new Stopwatch();
        private SoundPlayer SoundSpin;
        private SoundPlayer SoundResult;

        // 회전판 이미지 캐시
        private Bitmap cachedWheelImage = null;
        private Image btnSpinImage;
        private Image btnSpin2Image;
        private Image btnSpin3Image;

        // 사운드 파일 메모리 스트림
        private MemoryStream soundSpinStream;
        private MemoryStream soundResultStream;

        // 회전 중 여부
        private bool spinning = false;

        // 당첨자 이름
        private string winnerName = null;

        // 폼 생성자
        public Roulette()
        {
            try // [INIT]
            {
                InitializeComponent();

                // 폰트 통일
                try // [INIT-FONT]
                {
                    this.Font = new Font("맑은 고딕", 9F, FontStyle.Regular);
                    foreach (Control ctl in this.Controls)
                        ctl.Font = new Font("맑은 고딕", ctl.Font.Size, ctl.Font.Style);
                }
                catch (Exception ex) { LogError("[INIT-FONT] " + ex); }

                // SPIN 버튼 이미지 로드
                try // [INIT-SPINIMG]
                {
                    btnSpinImage = Image.FromStream(new MemoryStream(Properties.Resources.btnSPIN));
                    btnSpin2Image = Image.FromStream(new MemoryStream(Properties.Resources.btnSPIN2));
                    btnSpin3Image = Image.FromStream(new MemoryStream(Properties.Resources.btnSPIN3));
                    pbSpin.BackgroundImage = btnSpinImage;
                }
                catch (Exception ex) { LogError("[INIT-SPINIMG] " + ex); }

                // SPIN 버튼 Paint 이벤트 연결
                try { pbSpin.Paint += RemainSeconds; } catch (Exception ex) { LogError("[INIT-SPINPAINT] " + ex); }

                // SPIN 버튼을 회전판 위에 올리고 투명하게
                try
                {
                    pbSpin.BackColor = Color.Transparent;
                    pbSpin.Parent = pbWheel;
                    pbSpin.BringToFront();
                }
                catch (Exception ex) { LogError("[INIT-SPINPARENT] " + ex); }

                // 회전판 크기 변경 시 SPIN 버튼 위치 재조정
                try { pbWheel.Resize += (s, e) => CenterSpinButton(); } catch (Exception ex) { LogError("[INIT-WHEELRESIZE] " + ex); }

                // 회전 타이머 설정
                try
                {
                    spinTimer.Interval = 10; // 10ms마다 Tick (애니메이션 부드럽게)
                    spinTimer.Tick += SpinTimer_Tick;
                }
                catch (Exception ex) { LogError("[INIT-SPINTIMER] " + ex); }

                // 사운드 파일 로드
                try
                {
                    soundSpinStream = new MemoryStream(Properties.Resources.SoundSpin);
                    SoundSpin = new SoundPlayer(soundSpinStream);
                    SoundSpin.Load();
                }
                catch (Exception ex) { LogError("[INIT-SOUNDSPIN] " + ex); }
                try
                {
                    soundResultStream = new MemoryStream(Properties.Resources.SoundResult);
                    SoundResult = new SoundPlayer(soundResultStream);
                    SoundResult.Load();
                }
                catch (Exception ex) { LogError("[INIT-SOUNDRESULT] " + ex); }

                // 트랙바 이벤트 연결 및 초기화
                try
                {
                    tbSpinDuration.ValueChanged += TbSpinDuration_ValueChanged;
                    TbSpinDuration_ValueChanged(null, null);
                }
                catch (Exception ex) { LogError("[INIT-SPINDURATION] " + ex); }

                // 멤버/선물 파일 비동기 로드
                try { _ = LoadCsvFilesAsync(); } catch (Exception ex) { LogError("[INIT-LOADCSV] " + ex); }

                // 회전판 이미지 그리기
                try { RedrawWheel(); } catch (Exception ex) { LogError("[INIT-REDRAWWHEEL] " + ex); }
            }
            catch (Exception ex) { LogError("[INIT] " + ex); }
        }

        // 트랙바 값 변경 시 라벨 표시
        private void TbSpinDuration_ValueChanged(object sender, EventArgs e)
        {
            try // [SPINDURATION-LABEL]
            {
                lblSpinDuration.Text = $"{tbSpinDuration.Value}초";
            }
            catch (Exception ex) { LogError("[SPINDURATION-LABEL] " + ex); }
        }

        // 멤버/선물 CSV 파일 비동기 로드 및 UI 반영
        private async Task LoadCsvFilesAsync()
        {
            string[] memberLines;
            string[] giftLines;

            try // [LOADCSV-READ]
            {
                memberLines = File.Exists("member.csv") ? await Task.Run(() => File.ReadAllLines("member.csv")) : Array.Empty<string>();
                giftLines = File.Exists("gift.csv") ? await Task.Run(() => File.ReadAllLines("gift.csv")) : Array.Empty<string>();
            }
            catch (Exception ex)
            {
                LogError("[LOADCSV-READ] " + ex);
                MessageBox.Show("CSV 파일을 읽는 중 오류가 발생했습니다.\nCSV 파일을 닫고 다시 실행하세요.:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                memberLines = Array.Empty<string>();
                giftLines = Array.Empty<string>();
            }

            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            try // [LOADCSV-UIUPDATE]
            {
                await Task.Factory.StartNew(() =>
                {
                    try
                    {
                        nameList.Clear();
                        remainingNames.Clear();
                        selectedNames.Clear();
                        dgvMembers.Rows.Clear();
                        dgvGifts.Rows.Clear();
                    }
                    catch (Exception ex) { LogError("[LOADCSV-UIUPDATE-CLEAR] " + ex); }

                    foreach (var line in memberLines)
                    {
                        try
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
                        catch (Exception ex) { LogError("[LOADCSV-UIUPDATE-MEMBERROW] " + ex); }
                    }

                    foreach (var line in giftLines)
                    {
                        try
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
                        catch (Exception ex) { LogError("[LOADCSV-UIUPDATE-GIFTROW] " + ex); }
                    }
                }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception ex) { LogError("[LOADCSV-UIUPDATE] " + ex); }

            try { RedrawWheel(); } catch (Exception ex) { LogError("[LOADCSV-REDRAWWHEEL] " + ex); }
        }

        // 회전판 위에 바늘(삼각형) 그리기
        private void SelectionMarker(object sender, PaintEventArgs e)
        {
            try // [SELECTIONMARKER]
            {
                int w = pbWheel.Width;
                int cx = w / 2;
                int needleWidth = Math.Max(w, pbWheel.Height) / 50; // 바늘의 두께를 회전판 크기의 1/50로 설정 (비율 유지)
                int needleHeight = Math.Max(w, pbWheel.Height) / 40; // 바늘의 높이를 회전판 크기의 1/40로 설정 (비율 유지)
                PointF p1 = new PointF(cx - needleWidth / 2f, 0);
                PointF p2 = new PointF(cx + needleWidth / 2f, 0);
                PointF p3 = new PointF(cx, needleHeight);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush b = new SolidBrush(Color.DarkRed))
                    e.Graphics.FillPolygon(b, new[] { p1, p2, p3 });
                using (Pen pen = new Pen(SystemColors.Control, 2))
                    e.Graphics.DrawPolygon(pen, new[] { p1, p2, p3 });
            }
            catch (Exception ex) { LogError("[SELECTIONMARKER] " + ex); }
        }

        // 회전판 이미지 새로 그리기
        private void RedrawWheel()
        {
            try // [REDRAWWHEEL]
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
                    LogError("[REDRAWWHEEL-DRAW] " + ex);
                    MessageBox.Show("회전판 이미지를 생성할 수 없습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                UpdatePbSpinBackColor();
                CenterSpinButton();
            }
            catch (Exception ex) { LogError("[REDRAWWHEEL] " + ex); }
        }

        // 고해상도 회전판 이미지 생성
        private Bitmap DrawWheelImage(float currentAngle)
        {
            try // [DRAWWHEELIMAGE]
            {
                pbWheel.Paint -= SelectionMarker;
                pbWheel.Paint += SelectionMarker;

                int scale = 2; // 고해상도 이미지를 만들기 위한 배율 (2배)
                int size = (Math.Min(pbWheel.Width, pbWheel.Height) - 2) * scale; // 회전판의 크기를 컨트롤 크기 기준으로 정함
                Bitmap bmp = null;
                try { bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb); }
                catch (Exception ex) { LogError("[DRAWWHEELIMAGE-BITMAP] " + ex); throw; }

                try
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        float sectionAngle = 360f / Math.Max(1, remainingNames.Count); // 남은 멤버 수만큼 섹션을 균등 분할
                        float angleStart = currentAngle;

                        List<Brush> pastelBrushes = new List<Brush>();
                        for (int i = 0; i < remainingNames.Count; i++)
                        {
                            try { pastelBrushes.Add(new SolidBrush(GetRandomPastelColor())); }
                            catch (Exception ex) { LogError("[DRAWWHEELIMAGE-COLOR] " + ex); }
                        }

                        for (int i = 0; i < remainingNames.Count; i++)
                        {
                            try
                            {
                                Brush brush = pastelBrushes[i];
                                g.FillPie(brush, 0, 0, size, size, angleStart, sectionAngle);
                                g.DrawPie(Pens.White, 0, 0, size, size, angleStart, sectionAngle);

                                var midAngle = angleStart + sectionAngle / 2; // 섹션의 중앙 각도
                                double rad = midAngle * Math.PI / 180; // 각도를 라디안으로 변환

                                float centerX = size / 2f;
                                float centerY = size / 2f;
                                float outerRadius = size / 2f - 20 * scale; // 바깥쪽 반지름 (테두리 여백 확보)
                                float innerRadius = size / 4f + 10 * scale; // 안쪽 반지름 (중앙 구멍 여백 확보)
                                float textRadius = (outerRadius + innerRadius) / 2f; // 텍스트를 그릴 원의 반지름 (섹션 중앙)

                                float arcLength = (float)(Math.PI * 2 * textRadius * (sectionAngle / 360.0)); // 섹션 호의 길이 (텍스트 최대 폭)
                                float maxTextWidth = arcLength * 0.8f; // 텍스트가 섹션을 넘지 않도록 80%만 사용

                                float minFont = 5f * scale;
                                float maxFont = 12f * scale;
                                float fontSize = maxFont;
                                string text = remainingNames[i];
                                SizeF textSize;

                                using (Font testFont = new Font("맑은 고딕", fontSize, FontStyle.Bold))
                                { textSize = g.MeasureString(text, testFont); }
                                while (textSize.Width > maxTextWidth && fontSize > minFont)
                                {
                                    fontSize -= 0.5f * scale; // 텍스트가 섹션을 넘으면 폰트 크기를 줄임
                                    using (Font testFont = new Font("맑은 고딕", fontSize, FontStyle.Bold))
                                        textSize = g.MeasureString(text, testFont);
                                }

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
                            catch (Exception ex) { LogError("[DRAWWHEELIMAGE-SECTION] " + ex); }
                        }

                        try
                        {
                            int holeSize = (int)(Math.Min(pbWheel.Width, pbWheel.Height) * 0.2 * scale); // 중앙 구멍 크기를 회전판 크기의 20%로 설정
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
                        catch (Exception ex) { LogError("[DRAWWHEELIMAGE-HOLE] " + ex); }
                    }
                }
                catch (Exception ex) { LogError("[DRAWWHEELIMAGE-GRAPHICS] " + ex); throw; }

                try
                {
                    Bitmap resized = new Bitmap(bmp, Math.Min(pbWheel.Width, pbWheel.Height) - scale, Math.Min(pbWheel.Width, pbWheel.Height) - scale); // 실제 표시 크기로 다운샘플링
                    bmp.Dispose();
                    return resized;
                }
                catch (Exception ex) { LogError("[DRAWWHEELIMAGE-RESIZE] " + ex); bmp?.Dispose(); throw; }
            }
            catch (Exception ex) { LogError("[DRAWWHEELIMAGE] " + ex); throw; }
        }

        // 파스텔톤 랜덤 색상 생성
        private Color GetRandomPastelColor()
        {
            try // [GETPASTELCOLOR]
            {
                int r = random.Next(127, 256);
                int g = random.Next(127, 256);
                int b = random.Next(127, 256);
                r = (r + 255) / 2; // 밝은 파스텔톤을 만들기 위해 255와 평균
                g = (g + 255) / 2;
                b = (b + 255) / 2;
                return Color.FromArgb(r, g, b);
            }
            catch (Exception ex) { LogError("[GETPASTELCOLOR] " + ex); return Color.Gray; }
        }

        // SPIN 버튼 배경색을 회전판과 맞춤
        private void UpdatePbSpinBackColor()
        {
            try // [UPDATESPINBACK]
            {
                if (pbSpin.IsDisposed || !pbSpin.IsHandleCreated) return;
                if (pbWheel.Image is not Bitmap bmp) return;

                if (!pbSpin.IsDisposed && pbSpin.IsHandleCreated)
                {
                    var spinCenter = pbSpin.PointToScreen(new Point(pbSpin.Width / 2, pbSpin.Height / 2));
                    var wheelOrigin = pbWheel.PointToScreen(Point.Empty);

                    int x = spinCenter.X - wheelOrigin.X;
                    int y = spinCenter.Y - wheelOrigin.Y;

                    if (pbWheel.Image.Width != pbWheel.Width || pbWheel.Image.Height != pbWheel.Height)
                    {
                        x = x * pbWheel.Image.Width / pbWheel.Width; // 컨트롤 크기와 이미지 크기가 다를 때 비율 변환
                        y = y * pbWheel.Image.Height / pbWheel.Height;
                    }

                    if (x >= 0 && y >= 0 && x < bmp.Width && y < bmp.Height)
                    {
                        Color color = bmp.GetPixel(x, y);
                        pbSpin.BackColor = color;
                    }
                }
            }
            catch (Exception ex) { LogError("[UPDATESPINBACK] " + ex); }
        }

        // SPIN 버튼을 회전판 중앙에 위치
        private void CenterSpinButton()
        {
            try // [CENTERSPINBTN]
            {
                int size = (int)(Math.Min(pbWheel.Width, pbWheel.Height) * 0.25); // SPIN 버튼 크기를 회전판의 25%로 설정
                size = Math.Max(size, 30); // 최소 크기 30 보장
                pbSpin.Size = new Size(size, size);

                int centerX = pbWheel.Width / 2 - pbSpin.Width / 2; // 중앙 정렬
                int centerY = pbWheel.Height / 2 - pbSpin.Height / 2;
                pbSpin.Location = new Point(centerX, centerY);
            }
            catch (Exception ex) { LogError("[CENTERSPINBTN] " + ex); }
        }

        // SPIN 버튼 클릭 시 회전 시작
        private void btnSpin_Click(object sender, EventArgs e)
        {
            try // [SPINCLICK]
            {
                try { soundSpinStream.Position = 0; SoundSpin?.PlayLooping(); } catch (Exception ex) { LogError("[SPINCLICK-SOUND] " + ex); }
                try { pbWheel.Image = DrawWheelImage(angle); } catch (Exception ex) { LogError("[SPINCLICK-DRAWWHEEL] " + ex); }
                try { RedrawWheel(); } catch (Exception ex) { LogError("[SPINCLICK-REDRAW] " + ex); }
                try { pbSpin.Invalidate(); } catch (Exception ex) { LogError("[SPINCLICK-INVALIDATE] " + ex); }

                pbSpin.BackgroundImage = btnSpin3Image;
                winnerName = null;

                if (spinning || remainingNames.Count == 0) return;
                spinning = true;
                spinStopwatch.Restart();

                // 회전 시간(초) + 1 ~ 9.9초 랜덤 추가
                totalTime = (float)tbSpinDuration.Value + (random.Next(1, 100) * 0.1f); // 트랙바 값(초)에 0.1~9.9초 랜덤 추가로 매번 회전 시간이 달라짐

                // 회전수 계산 (랜덤성 부여)
                float baseRotations = (totalTime * 0.6f) * (random.Next(80, 301) * 0.01f); // 전체 시간의 60%에 0.8~3.0배 랜덤 곱 (기본 회전수)
                float boostFactor = 1.0f * (random.Next(100, 301) * 0.01f); // 1.0~3.0배 랜덤 가중치 (추가 회전수에 사용)
                float timeThreshold = 10f * (random.Next(50, 201) * 0.01f); // 5~20초 랜덤 (추가 회전수 적용 임계값)
                float timeRatio = Math.Clamp((totalTime - timeThreshold) / timeThreshold, 0f, 1f); // 임계값 초과 시에만 추가 회전수 비율 적용
                float extraRotations = timeRatio * boostFactor * totalTime * (random.Next(80, 251) * 0.01f); // 추가 회전수: 비율 * 가중치 * 시간 * 0.8~2.5배 랜덤

                float rotations = baseRotations + extraRotations; // 최종 회전수 = 기본 + 추가
                totalAngle = 360f * rotations; // 전체 회전 각도 = 회전수 * 360도

                angle = 0f;
                elapsedTime = 0f;

                try { spinTimer.Start(); } catch (Exception ex) { LogError("[SPINCLICK-TIMERSTART] " + ex); }
            }
            catch (Exception ex) { LogError("[SPINCLICK] " + ex); }
        }

        // 타이머 Tick마다 회전 애니메이션 처리
        private void SpinTimer_Tick(object sender, EventArgs e)
        {
            try { ThreadSpinTimer_Tick(); } catch (Exception ex) { LogError("[SPINTIMER-TICK] " + ex); }
        }
        private void ThreadSpinTimer_Tick()
        {
            if (InvokeRequired)
            {
                _ = Task.Factory.StartNew(() => threadSpinTimer_Tick(), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
                return;
            }
            threadSpinTimer_Tick();
        }
        private void threadSpinTimer_Tick()
        {
            try // [SPINTIMER-THREADTICK]
            {
                try { elapsedTime = spinStopwatch.ElapsedMilliseconds / 1000f; }
                catch (Exception ex)
                {
                    LogError("[SPINTIMER-ELAPSED] " + ex);
                    MessageBox.Show("회전 중 오류(1)):\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    try { spinTimer.Stop(); } catch (Exception e) { LogError("[SPINTIMER-STOP1] " + e); }
                    spinning = false;
                }

                if (elapsedTime >= totalTime)
                {
                    elapsedTime = totalTime;
                    try { spinTimer.Stop(); } catch (Exception ex) { LogError("[SPINTIMER-STOP2] " + ex); }
                    try { SoundSpin?.Stop(); } catch (Exception ex) { LogError("[SPINTIMER-SOUNDSTOP] " + ex); }
                    try { soundResultStream.Position = 0; SoundResult?.Play(); } catch (Exception ex) { LogError("[SPINTIMER-RESULTSOUND] " + ex); }
                    spinning = false;
                    try
                    {
                        string result = GetCurrentSelectedName();
                        selectedNames.Add(result);
                        ProcessRouletteResult(result, selectedNames.Count);
                        remainingNames.Remove(result);

                        winnerName = "Win!\n" + result;
                        pbSpin.Invalidate();
                    }
                    catch (Exception ex)
                    {
                        LogError("[SPINTIMER-RESULT] " + ex);
                        MessageBox.Show("회전 중 오류(2)):\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        try { spinTimer.Stop(); } catch (Exception e) { LogError("[SPINTIMER-STOP3] " + e); }
                        spinning = false;
                    }
                    return;
                }

                try
                {
                    float progress = elapsedTime / totalTime; // 전체 진행률 (0~1)
                    float adjustedProgress = (float)Math.Pow(progress, accelerationFactor); // 가속 곡선 적용 (초반 빠르게)
                    float eased = 1f - (float)Math.Pow(1f - adjustedProgress, decelerationFactor); // 감속 곡선 적용 (마지막 천천히)

                    angle = eased * totalAngle; // 최종 각도 = 감속 곡선 * 전체 회전 각도
                    angle %= 360f; // 0~360도 내로 정규화

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
                    LogError("[SPINTIMER-ROTATE] " + ex);
                    MessageBox.Show("회전 중 오류(3)):\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    try { spinTimer.Stop(); } catch (Exception e) { LogError("[SPINTIMER-STOP4] " + e); }
                    spinning = false;
                }
                UpdatePbSpinBackColor();
                pbSpin.Invalidate();
            }
            catch (Exception ex) { LogError("[SPINTIMER-THREADTICK] " + ex); }
        }

        // 이미지를 지정 각도만큼 회전
        private Bitmap RotateImage(Bitmap src, float angle)
        {
            try // [ROTATEIMAGE]
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
            catch (Exception ex) { LogError("[ROTATEIMAGE] " + ex); return null; }
        }

        // SPIN 버튼에 남은 시간/당첨자 이름 표시
        private void RemainSeconds(object sender, PaintEventArgs e)
        {
            try // [REMAINSECONDS]
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (spinning)
                {
                    float remain = Math.Max(0, totalTime - elapsedTime); // 남은 시간(초)
                    string text = ((int)remain).ToString("D3");
                    float fontSize = Math.Max(3, pbSpin.Height / 4f);

                    Color textColor;
                    if (remain < 1.0f)
                        textColor = Color.White;
                    else if (remain < 3f)
                        textColor = Color.Red;
                    else
                    {
                        float t = Math.Clamp((totalTime - remain) / (totalTime - 3f), 0f, 1f); // 3초 이상일 때 검정~빨강 그라데이션
                        int r = (int)(255 * t);
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
                        while (textSize.Width > pbSpin.Width * 0.9f && fontSize > minFontSize)
                        {
                            fontSize -= 1f; // 텍스트가 버튼을 넘지 않도록 폰트 크기 줄임
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
                    int memberCount = remainingNames.Count;
                    int giftCount = 0;
                    foreach (DataGridViewRow row in dgvGifts.Rows)
                    {
                        if (row.IsNewRow) continue;
                        if (string.IsNullOrEmpty(row.Cells["gMemberColumn"].Value?.ToString()))
                            giftCount++;
                    }
                    double probability = (memberCount > 0) ? (giftCount * 100.0 / memberCount) : 0.0; // 남은 멤버 대비 남은 선물 확률(%) 계산
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
                LogError("[REMAINSECONDS] " + ex);
                try { File.AppendAllText("error.log", $"{DateTime.Now}: RemainSeconds: {ex}\n"); } catch { }
            }
        }

        // 현재 바늘이 가리키는 멤버 이름 반환
        private string GetCurrentSelectedName()
        {
            try // [GETCURRENTSELECTEDNAME]
            {
                if (remainingNames.Count == 0) return "";
                float sectionAngle = 360f / remainingNames.Count; // 각 멤버가 차지하는 각도 (360도 균등 분할)
                float needleOffset = 270; // 바늘이 12시 방향(270도) 기준
                float normalizedAngle = (needleOffset - angle + 360) % 360; // 현재 각도를 0~360도로 정규화
                int index = (int)(normalizedAngle / sectionAngle); // 바늘이 가리키는 섹션의 인덱스
                return remainingNames[index];
            }
            catch (Exception ex)
            {
                LogError("[GETCURRENTSELECTEDNAME] " + ex);
                return "";
            }
        }

        // 당첨 결과 처리 및 저장
        private async void ProcessRouletteResult(string selectedMember, int order)
        {
            try // [PROCESSROULETTERESULT]
            {
                for (int i = 0; i < dgvMembers.Rows.Count; i++)
                {
                    try
                    {
                        if ((string)dgvMembers.Rows[i].Cells["mMemberColumn"].Value == selectedMember)
                        {
                            dgvMembers.Rows[i].Cells["mResultColumn"].Value = order.ToString();
                            dgvMembers.ClearSelection();
                            dgvMembers.Rows[i].Selected = true;
                            dgvMembers.CurrentCell = dgvMembers.Rows[i].Cells["mMemberColumn"];
                            break;
                        }
                    }
                    catch (Exception ex) { LogError("[PROCESSROULETTERESULT-MEMBERROW] " + ex); }
                }
                await SaveMembersToCsv();

                for (int i = 0; i < dgvGifts.Rows.Count; i++)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(dgvGifts.Rows[i].Cells["gMemberColumn"].Value?.ToString()))
                        {
                            dgvGifts.Rows[i].Cells["gMemberColumn"].Value = selectedMember;
                            dgvGifts.ClearSelection();
                            dgvGifts.Rows[i].Selected = true;
                            dgvGifts.CurrentCell = dgvGifts.Rows[i].Cells["gGiftColumn"];
                            break;
                        }
                    }
                    catch (Exception ex) { LogError("[PROCESSROULETTERESULT-GIFTROW] " + ex); }
                }
                await SaveGiftsToCsv();
            }
            catch (Exception ex) { LogError("[PROCESSROULETTERESULT] " + ex); }
        }

        // 멤버 정보를 CSV로 저장
        private async Task SaveMembersToCsv()
        {
            try // [SAVEMEMBERSCSV]
            {
                var lines = new List<string>();
                foreach (DataGridViewRow row in dgvMembers.Rows)
                {
                    try
                    {
                        if (row.IsNewRow) continue;
                        string name = row.Cells["mMemberColumn"].Value?.ToString() ?? "";
                        string result = row.Cells["mResultColumn"].Value?.ToString() ?? "";
                        lines.Add($"{name},{result}");
                    }
                    catch (Exception ex) { LogError("[SAVEMEMBERSCSV-ROW] " + ex); }
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
                    LogError("[SAVEMEMBERSCSV-WRITE] " + ex);
                    if (SynchronizationContext.Current == null)
                        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                    await Task.Factory.StartNew(() =>
                    {
                        MessageBox.Show("CSV 파일을 쓰는 중 오류가 발생했습니다.\n실행중에는 CSV 파일을 열지 마세요.:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
            catch (Exception ex) { LogError("[SAVEMEMBERSCSV] " + ex); }
        }

        // 선물 정보를 CSV로 저장
        private async Task SaveGiftsToCsv()
        {
            try // [SAVEGIFTSCSV]
            {
                var lines = new List<string>();
                foreach (DataGridViewRow row in dgvGifts.Rows)
                {
                    try
                    {
                        if (row.IsNewRow) continue;
                        string gift = row.Cells["gGiftColumn"].Value?.ToString() ?? "";
                        string member = row.Cells["gMemberColumn"].Value?.ToString() ?? "";
                        lines.Add($"{gift},{member}");
                    }
                    catch (Exception ex) { LogError("[SAVEGIFTSCSV-ROW] " + ex); }
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
                    LogError("[SAVEGIFTSCSV-WRITE] " + ex);
                    if (SynchronizationContext.Current == null)
                        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                    await Task.Factory.StartNew(() =>
                    {
                        MessageBox.Show("CSV 파일을 쓰는 중 오류가 발생했습니다.\n실행중에는 CSV 파일을 열지 마세요.:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
            catch (Exception ex) { LogError("[SAVEGIFTSCSV] " + ex); }
        }

        // 폼 크기 변경 시 회전판/버튼 재배치
        private void Roulette_Resize(object sender, EventArgs e)
        {
            try { RedrawWheel(); } catch (Exception ex) { LogError("[RESIZE] " + ex); }
        }

        // 멤버 추가 버튼 클릭 시 멤버 추가
        private void btnAddMembers_Click(object sender, EventArgs e)
        {
            try // [ADDMEMBER]
            {
                string name = txtAddMembers.Text.Trim();
                if (!string.IsNullOrEmpty(name) && !nameList.Contains(name))
                {
                    int rowIndex = -1;
                    try
                    {
                        rowIndex = dgvMembers.Rows.Add();
                        dgvMembers.Rows[rowIndex].Cells["mMemberColumn"].Value = name;
                        dgvMembers.Rows[rowIndex].Cells["mResultColumn"].Value = "";
                        nameList.Add(name);
                        remainingNames.Add(name);
                    }
                    catch (Exception ex) { LogError("[ADDMEMBER-DGV] " + ex); MessageBox.Show("멤버 추가 중 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }

                    try { txtAddMembers.Clear(); } catch (Exception ex) { LogError("[ADDMEMBER-TXTCLR] " + ex); }
                    try { _ = SaveMembersToCsv(); } catch (Exception ex) { LogError("[ADDMEMBER-SAVECSV] " + ex); }
                    try { RedrawWheel(); } catch (Exception ex) { LogError("[ADDMEMBER-REDRAW] " + ex); }
                }
            }
            catch (Exception ex) { LogError("[ADDMEMBER] " + ex); MessageBox.Show("멤버 추가 처리 중 예기치 못한 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // 선물 추가 버튼 클릭 시 선물 추가
        private void btnAddGifts_Click(object sender, EventArgs e)
        {
            try // [ADDGIFT]
            {
                string gift = txtAddGifts.Text.Trim();
                bool exists = false;
                try
                {
                    foreach (DataGridViewRow row in dgvGifts.Rows)
                    {
                        if ((row.Cells["gGiftColumn"].Value?.ToString() ?? "") == gift)
                        {
                            exists = true;
                            break;
                        }
                    }
                }
                catch (Exception ex) { LogError("[ADDGIFT-CHECKDUP] " + ex); }

                if (!string.IsNullOrEmpty(gift) && !exists)
                {
                    int rowIndex = -1;
                    try
                    {
                        rowIndex = dgvGifts.Rows.Add();
                        dgvGifts.Rows[rowIndex].Cells["gGiftColumn"].Value = gift;
                        dgvGifts.Rows[rowIndex].Cells["gMemberColumn"].Value = "";
                    }
                    catch (Exception ex) { LogError("[ADDGIFT-DGV] " + ex); MessageBox.Show("선물 추가 중 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }

                    try { txtAddGifts.Clear(); } catch (Exception ex) { LogError("[ADDGIFT-TXTCLR] " + ex); }
                    try { _ = SaveGiftsToCsv(); } catch (Exception ex) { LogError("[ADDGIFT-SAVECSV] " + ex); }
                }
            }
            catch (Exception ex) { LogError("[ADDGIFT] " + ex); MessageBox.Show("선물 추가 처리 중 예기치 못한 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // SPIN 버튼에 마우스 올리면 이미지 변경
        private void pbSpin_MouseHover(object sender, EventArgs e)
        {
            try // [SPINHOVER]
            {
                if (!spinning)
                {
                    pbSpin.BackgroundImage = btnSpin2Image;
                    winnerName = null;
                }
            }
            catch (Exception ex) { LogError("[SPINHOVER] " + ex); }
        }
        // SPIN 버튼에서 마우스 떼면 이미지 원래대로
        private void pbSpin_MouseLeave(object sender, EventArgs e)
        {
            try // [SPINLEAVE]
            {
                if (!spinning)
                {
                    pbSpin.BackgroundImage = btnSpinImage;
                    winnerName = null;
                }
            }
            catch (Exception ex) { LogError("[SPINLEAVE] " + ex); }
        }

        // 에러 로그 파일 기록
        private void LogError(string message)
        {
            try
            {
                File.AppendAllText("error.log", $"\n\n\n{DateTime.Now}: {message}\n");
            }
            catch (Exception) { /* 로그 기록 실패시 재귀 방지 */ }
        }

        // 종료 버튼 클릭 시 프로그램 종료
        private void btnExit_Click(object sender, EventArgs e)
        {
            try // [EXIT]
            {
                this.Close();
                this.Dispose();
            }
            catch (Exception ex) { LogError("[EXIT] " + ex); }
        }
    }
}