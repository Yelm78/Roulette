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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Net.Mime.MediaTypeNames;
using System.Collections.Concurrent;

namespace Roulette
{
    public partial class Roulette : Form
    {
        #region 변수선언
        // 파일 동시 접근 방지
        private static readonly object memberCsvLock = new object();
        private static readonly object giftCsvLock = new object();
        private static readonly object logLock = new object();

        private static readonly BlockingCollection<string> logQueue = new BlockingCollection<string>(new ConcurrentQueue<string>());
        private static readonly CancellationTokenSource logCts = new CancellationTokenSource();
        private static Task logTask;

        private bool isLoading = false;

        // 멤버, 남은 멤버, 당첨 멤버 리스트
        private List<string> nameList = new List<string>();
        private List<string> remainingNames = new List<string>();
        private List<string> selectedNames = new List<string>();

        // DataGridView 컬럼(수정전)
        private string prevMemberName = "";
        private string prevGiftName = "";

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
        private System.Drawing.Image btnSpinImage;
        private System.Drawing.Image btnSpin2Image;
        private System.Drawing.Image btnSpin3Image;

        // 사운드 파일 메모리 스트림
        private MemoryStream soundSpinStream;
        private MemoryStream soundResultStream;

        // 회전 중 여부
        private bool spinning = false;

        // 당첨자 이름
        private string winnerName = null;
        #endregion

        // 폼 생성자
        public Roulette()
        {
            try // [INIT]
            {
                InitializeComponent();

                if (logTask == null)
                {
                    logTask = Task.Run(() => LogWorker(logCts.Token));
                }

                LogWrite("[Application Launch] ");
                KeyPreview = true;

                // 폼과 모든 컨트롤의 폰트를 "맑은 고딕"으로 통일
                try { this.Font = new System.Drawing.Font("맑은 고딕", 9F, FontStyle.Regular); foreach (Control ctl in this.Controls) ctl.Font = new System.Drawing.Font("맑은 고딕", ctl.Font.Size, ctl.Font.Style); } catch (Exception ex) { LogWrite("[INIT-FONT] " + ex); }

                // SPIN 버튼에 사용할 이미지 3종을 메모리에서 불러옴
                try { btnSpinImage = System.Drawing.Image.FromStream(new MemoryStream(Properties.Resources.btnSPIN)); btnSpin2Image = System.Drawing.Image.FromStream(new MemoryStream(Properties.Resources.btnSPIN2)); btnSpin3Image = System.Drawing.Image.FromStream(new MemoryStream(Properties.Resources.btnSPIN3)); pbSpin.BackgroundImage = btnSpinImage; } catch (Exception ex) { LogWrite("[INIT-SPINIMG] " + ex); }

                // SPIN 버튼에 남은 시간/당첨자 표시를 위한 Paint 이벤트 연결
                try { pbSpin.Paint += RemainSeconds; } catch (Exception ex) { LogWrite("[INIT-SPINPAINT] " + ex); }

                // SPIN 버튼을 회전판 위에 올리고, 배경을 투명하게 설정
                try { pbSpin.BackColor = Color.Transparent; pbSpin.Parent = pbWheel; pbSpin.BringToFront(); } catch (Exception ex) { LogWrite("[INIT-SPINPARENT] " + ex); }

                // 회전판 크기가 바뀔 때마다 SPIN 버튼 위치를 중앙으로 재조정
                try { pbWheel.Resize += (s, e) => CenterSpinButton(); } catch (Exception ex) { LogWrite("[INIT-WHEELRESIZE] " + ex); }

                // 회전판 그리기 이벤트 연결 (Paint 이벤트)
                try { pbWheel.Paint += pbWheel_Paint; } catch (Exception ex) { LogWrite("[INIT-WHEELPAINT] " + ex); }

                // 회전 애니메이션 타이머 설정 (10ms마다 Tick 발생)
                try { spinTimer.Interval = 10; spinTimer.Tick += SpinTimer_Tick; } catch (Exception ex) { LogWrite("[INIT-SPINTIMER] " + ex); }

                // 사운드 파일을 메모리에서 불러와서 SoundPlayer에 등록
                try { soundSpinStream = new MemoryStream(Properties.Resources.SoundSpin); SoundSpin = new SoundPlayer(soundSpinStream); SoundSpin.Load(); } catch (Exception ex) { LogWrite("[INIT-SOUNDSPIN] " + ex); }
                try { soundResultStream = new MemoryStream(Properties.Resources.SoundResult); SoundResult = new SoundPlayer(soundResultStream); SoundResult.Load(); } catch (Exception ex) { LogWrite("[INIT-SOUNDRESULT] " + ex); }

                // 트랙바 값이 바뀔 때마다 라벨에 표시
                try { tbSpinDuration.ValueChanged += TbSpinDuration_ValueChanged; TbSpinDuration_ValueChanged(null, null); } catch (Exception ex) { LogWrite("[INIT-SPINDURATION] " + ex); }

                // 멤버/선물 CSV 파일을 비동기로 읽어와서 UI에 반영
                try { _ = LoadCsvFilesAsync(); } catch (Exception ex) { LogWrite("[INIT-LOADCSV] " + ex); }

                //// 회전판 이미지 그리기 (최초 1회)
                //try { RedrawWheel(); } catch (Exception ex) { LogError("[INIT-REDRAWWHEEL] " + ex); }
            }
            catch (Exception ex) { LogWrite("[INIT] " + ex); }
        }

        // 화살표키 이벤트 처리(트랙바 값 조정)
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            try
            {
                if (keyData == Keys.Left)
                {
                    if (tbSpinDuration.Value > tbSpinDuration.Minimum)
                        tbSpinDuration.Value = Math.Max(tbSpinDuration.Minimum, tbSpinDuration.Value - 1);
                    return true;
                }
                else if (keyData == Keys.Right)
                {
                    if (tbSpinDuration.Value < tbSpinDuration.Maximum)
                        tbSpinDuration.Value = Math.Min(tbSpinDuration.Maximum, tbSpinDuration.Value + 1);
                    return true;
                }
                else if (keyData == Keys.Down)
                {
                    // 10단위로 먼저 맞추기
                    int v = tbSpinDuration.Value;
                    int mod = v % 10;
                    if (mod != 0)
                    {
                        v -= mod;
                        if (v < tbSpinDuration.Minimum) v = tbSpinDuration.Minimum;
                        tbSpinDuration.Value = v;
                    }
                    else if (v > tbSpinDuration.Minimum)
                    {
                        tbSpinDuration.Value = Math.Max(tbSpinDuration.Minimum, v - 10);
                    }
                    return true;
                }
                else if (keyData == Keys.Up)
                {
                    // 10단위로 먼저 맞추기
                    int v = tbSpinDuration.Value;
                    int mod = v % 10;
                    if (mod != 0)
                    {
                        v += (10 - mod);
                        if (v > tbSpinDuration.Maximum) v = tbSpinDuration.Maximum;
                        tbSpinDuration.Value = v;
                    }
                    else if (v < tbSpinDuration.Maximum)
                    {
                        tbSpinDuration.Value = Math.Min(tbSpinDuration.Maximum, v + 10);
                    }
                    return true;
                }
            }
            catch (Exception ex) { LogWrite("[PROCESSCMDKEY-SPINDURATION] " + ex); }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // 트랙바 값 변경 시 라벨 표시
        private void TbSpinDuration_ValueChanged(object sender, EventArgs e)
        {
            try // [SPINDURATION-LABEL]
            {
                lblSpinDuration.Text = $"{tbSpinDuration.Value}초";
            }
            catch (Exception ex) { LogWrite("[SPINDURATION-LABEL] " + ex); }
        }

        // 멤버/선물 CSV 파일 비동기 로드 및 UI 반영
        private async Task LoadCsvFilesAsync()
        {
            isLoading = true; // 로딩 시작

            string[] memberLines;
            string[] giftLines;

            try // [LOADCSV-READ]
            {
                memberLines = File.Exists("members.csv") ? await Task.Run(() => File.ReadAllLines("members.csv")) : Array.Empty<string>();
                giftLines = File.Exists("gifts.csv") ? await Task.Run(() => File.ReadAllLines("gifts.csv")) : Array.Empty<string>();
            }
            catch (Exception ex)
            {
                LogWrite("[LOADCSV-READ] " + ex);
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

                        // DataGridView 업데이트 최적화
                        dgvMembers.SuspendLayout();
                        dgvGifts.SuspendLayout();
                        dgvMembers.Rows.Clear();
                        dgvGifts.Rows.Clear();

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
                            catch (Exception ex) { LogWrite("[LOADCSV-UIUPDATE-MEMBERROW] " + ex); }
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
                            catch (Exception ex) { LogWrite("[LOADCSV-UIUPDATE-GIFTROW] " + ex); }
                        }
                        dgvMembers.ResumeLayout();
                        dgvGifts.ResumeLayout();
                    }
                    catch (Exception ex) { LogWrite("[LOADCSV-UIUPDATE-CLEAR] " + ex); }
                }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception ex) { LogWrite("[LOADCSV-UIUPDATE] " + ex); }

            isLoading = false; // 로딩 끝

            try { await Task.Run(() => RedrawWheel()); } catch (Exception ex) { LogWrite("[LOADCSV-REDRAWWHEEL] " + ex); }
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
            catch (Exception ex) { LogWrite("[SELECTIONMARKER] " + ex); }
        }

        // 회전판 이미지 새로 그리기
        // 회전판에 표시될 섹션(멤버 이름 등)을 모두 그려서 Bitmap으로 만들어 저장
        // spinning(회전 중)일 때는 새로 그리지 않음 (메모리 누수 방지)
        private async void RedrawWheel()
        {
            if (spinning) return; // 회전 중에는 새로 그리지 않음

            try // [REDRAWWHEEL]
            {
                angle = 0;
                // 기존 이미지가 있으면 메모리 해제
                if (pbWheel.Image != null && pbWheel.Image != cachedWheelImage)
                    pbWheel.Image.Dispose();
                pbWheel.Image = null; // 이전 이미지 참조 해제
                cachedWheelImage?.Dispose();

                // 비동기 이미지 생성
                Bitmap newImage = null;
                try
                {
                    newImage = await Task.Run(() => DrawWheelImage(angle));
                }
                catch (Exception ex)
                {
                    LogWrite("[REDRAWWHEEL-ASYNC] " + ex);
                }

                // UI 스레드에서 이미지 적용
                if (pbWheel.InvokeRequired)
                {
                    pbWheel.Invoke(new Action(() =>
                    {
                        pbWheel.Image = newImage;
                        UpdatePbSpinBackColor();
                        CenterSpinButton();
                    }));
                }
                else
                {
                    pbWheel.Image = newImage;
                    UpdatePbSpinBackColor();
                    CenterSpinButton();
                }
            }
            catch (Exception ex) { LogWrite("[REDRAWWHEEL] " + ex); }
        }

        // 고해상도 회전판 이미지 생성 (남은 멤버 수에 따라 섹션을 나누고, 각 섹션에 이름 draw)
        private Bitmap DrawWheelImage(float currentAngle)
        {
            try // [DRAWWHEELIMAGE]
            {
                if (remainingNames.Count == 0)
                {
                    return new Bitmap(Math.Max(1, pbWheel.Width), Math.Max(1, pbWheel.Height));
                }

                pbWheel.Paint -= SelectionMarker;
                pbWheel.Paint += SelectionMarker;

                int scale = 2; // 고해상도 이미지를 만들기 위한 배율 (2배)
                int size = (Math.Min(pbWheel.Width, pbWheel.Height) - 2) * scale; // 회전판의 크기를 컨트롤 크기 기준으로 정함
                Bitmap bmp = null;
                try { bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb); }
                catch (Exception ex) { LogWrite("[DRAWWHEELIMAGE-BITMAP] " + ex); throw; }

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
                            // 각 섹션마다 파스텔톤 색상 브러시 생성
                            try { pastelBrushes.Add(new SolidBrush(GetRandomPastelColor())); }
                            catch (Exception ex) { LogWrite("[DRAWWHEELIMAGE-COLOR] " + ex); }
                        }

                        int count = Math.Min(remainingNames.Count, pastelBrushes.Count);
                        for (int i = 0; i < count; i++)
                        {
                            // 각 멤버 이름을 섹션에 그림
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

                                using (System.Drawing.Font testFont = new System.Drawing.Font("맑은 고딕", fontSize, FontStyle.Bold))
                                { textSize = g.MeasureString(text, testFont); }
                                while (textSize.Width > maxTextWidth && fontSize > minFont)
                                {
                                    fontSize -= 0.5f * scale; // 텍스트가 섹션을 넘으면 폰트 크기를 줄임
                                    using (System.Drawing.Font testFont = new System.Drawing.Font("맑은 고딕", fontSize, FontStyle.Bold))
                                        textSize = g.MeasureString(text, testFont);
                                }

                                using (System.Drawing.Font font = new System.Drawing.Font("맑은 고딕", fontSize, FontStyle.Bold))
                                {
                                    float x = (float)(centerX + Math.Cos(rad) * textRadius);
                                    float y = (float)(centerY + Math.Sin(rad) * textRadius);

                                    g.TranslateTransform(x, y);
                                    g.RotateTransform((float)midAngle);
                                    g.DrawString(text, font, Brushes.Black, -textSize.Width / 2, -textSize.Height / 2);
                                    g.ResetTransform();
                                }
                                brush.Dispose();
                                angleStart += sectionAngle;
                            }
                            catch (Exception ex) { LogWrite("[DRAWWHEELIMAGE-SECTION] " + ex); }
                        }

                        try
                        {
                            // 중앙 구멍(원)을 그림
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
                        catch (Exception ex) { LogWrite("[DRAWWHEELIMAGE-HOLE] " + ex); }
                        pastelBrushes.Clear();
                    }
                }
                catch (Exception ex) { LogWrite("[DRAWWHEELIMAGE-GRAPHICS] " + ex); throw; }

                try
                {
                    // 실제 표시 크기로 다운샘플링 (고해상도 -> 실제 크기)
                    Bitmap resized = new Bitmap(bmp, Math.Min(pbWheel.Width, pbWheel.Height) - scale, Math.Min(pbWheel.Width, pbWheel.Height) - scale);
                    bmp.Dispose();
                    return resized;
                }
                catch (Exception ex) { LogWrite("[DRAWWHEELIMAGE-RESIZE] " + ex); bmp?.Dispose(); throw; }
            }
            catch (Exception ex) { LogWrite("[DRAWWHEELIMAGE] " + ex); throw; }
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
            catch (Exception ex) { LogWrite("[GETPASTELCOLOR] " + ex); return Color.Gray; }
        }

        // SPIN 버튼 배경색을 회전판과 맞춤
        private void UpdatePbSpinBackColor()
        {
            try // [UPDATESPINBACK]
            {
                if (this.WindowState != FormWindowState.Minimized)
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
            }
            catch (Exception ex) { LogWrite("[UPDATESPINBACK] " + ex); }
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
            catch (Exception ex) { LogWrite("[CENTERSPINBTN] " + ex); }
        }

        // SPIN 버튼 클릭 시 회전 시작(엔터키 입력시 데이터 그리드뷰 값 입력)
        private void btnSpin_Click(object sender, EventArgs e)
        {
            if (txtAddMembers.TextLength > 0) { btnAddMembers_Click(sender, e); /* 텍스트 박스에 입력된 멤버 추가 */ return; /* 멤버 추가 후 회전 시작하지 않음 */ }
            if (txtAddGifts.TextLength > 0) { btnAddGifts_Click(sender, e); /* 텍스트 박스에 입력된 선물 추가 */ return; /* 선물 추가 후 회전 시작하지 않음 */ }

            try // [SPINCLICK]
            {
                try { soundSpinStream.Position = 0; SoundSpin?.PlayLooping(); } catch (Exception ex) { LogWrite("[SPINCLICK-SOUND] " + ex); }
                //try { pbWheel.Image = DrawWheelImage(angle); } catch (Exception ex) { LogError("[SPINCLICK-DRAWWHEEL] " + ex); }
                //try { RedrawWheel(); } catch (Exception ex) { LogError("[SPINCLICK-REDRAW] " + ex); }
                try { pbSpin.Invalidate(); } catch (Exception ex) { LogWrite("[SPINCLICK-INVALIDATE] " + ex); }

                pbSpin.BackgroundImage = null;
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

                LogWrite("[Start Spin] SpinTime(s): " + tbSpinDuration.Value.ToString() + "+" + (totalTime - (float)tbSpinDuration.Value).ToString() + " / r:" + totalAngle.ToString());

                angle = 0f;
                elapsedTime = 0f;

                try { spinTimer.Start(); } catch (Exception ex) { LogWrite("[SPINCLICK-TIMERSTART] " + ex); }
            }
            catch (Exception ex) { LogWrite("[SPINCLICK] " + ex); }
        }

        // 타이머 Tick마다 회전 애니메이션 처리
        private void SpinTimer_Tick(object sender, EventArgs e)
        {
            try { ThreadSpinTimer_Tick(); } catch (Exception ex) { LogWrite("[SPINTIMER-TICK] " + ex); }
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
                    LogWrite("[SPINTIMER-ELAPSED] " + ex);
                    MessageBox.Show("회전 중 오류(1)):\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    try { spinTimer.Stop(); } catch (Exception e) { LogWrite("[SPINTIMER-STOP1] " + e); }
                    spinning = false;
                }

                // 회전 중 메모리 사용량 기록
                if (spinning)
                {
                    try
                    {
                        var proc = Process.GetCurrentProcess();
                        long workingSet = proc.WorkingSet64;
                        long privateBytes = proc.PrivateMemorySize64;
                        //LogError($"[MEMORY] WS={workingSet / 1048576}MB, PM={privateBytes / 1048576}MB");
                    }
                    catch (Exception ex)
                    {
                        //LogError("[MEMORY-LOG] " + ex);
                    }
                }

                if (elapsedTime >= totalTime)
                {
                    elapsedTime = totalTime;
                    try { spinTimer.Stop(); } catch (Exception ex) { LogWrite("[SPINTIMER-STOP2] " + ex); }
                    try { SoundSpin?.Stop(); } catch (Exception ex) { LogWrite("[SPINTIMER-SOUNDSTOP] " + ex); }
                    try { soundResultStream.Position = 0; SoundResult?.Play(); } catch (Exception ex) { LogWrite("[SPINTIMER-RESULTSOUND] " + ex); }
                    spinning = false;
                    try
                    {
                        string result = GetCurrentSelectedName();
                        selectedNames.Add(result);
                        ProcessRouletteResult(result, selectedNames.Count);
                        remainingNames.Remove(result);

                        winnerName = "Win!\n" + result;
                        pbSpin.Invalidate();
                        LogWrite("Selected Member: " + result);
                    }
                    catch (Exception ex)
                    {
                        LogWrite("[SPINTIMER-RESULT] " + ex);
                        MessageBox.Show("회전 중 오류(2)):\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        try { spinTimer.Stop(); } catch (Exception e) { LogWrite("[SPINTIMER-STOP3] " + e); }
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

                    pbWheel.Invalidate(); /* Paint에서 회전만 적용 */
                    //// 회전 애니메이션 중
                    //if (cachedWheelImage != null)
                    //{
                    //    var oldImage = pbWheel.Image;
                    //    var rotated = RotateImage(cachedWheelImage, angle);
                    //    pbWheel.Image = rotated;

                    //    if (oldImage != null && oldImage != cachedWheelImage)
                    //        oldImage.Dispose();
                    //}
                }
                catch (Exception ex)
                {
                    LogWrite("[SPINTIMER-ROTATE] " + ex);
                    MessageBox.Show("회전 중 오류(3)):\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    try { spinTimer.Stop(); } catch (Exception e) { LogWrite("[SPINTIMER-STOP4] " + e); }
                    spinning = false;
                }
                UpdatePbSpinBackColor();
                pbSpin.Invalidate();
            }
            catch (Exception ex) { LogWrite("[SPINTIMER-THREADTICK] " + ex); }
        }

        // private Bitmap RotateImage(Bitmap src, float angle) 대체, 메모리 누수 문제로 수정함
        // cachedWheelImage를 사용, 회전 애니메이션은 Graphics의 Transform 으로 처리
        private void pbWheel_Paint(object sender, PaintEventArgs e)
        {
            if (cachedWheelImage != null)
            {
                // 그래픽 품질을 부드럽게 설정
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // 회전판의 중심 좌표 계산
                float cx = pbWheel.Width / 2f;
                float cy = pbWheel.Height / 2f;

                // 중심으로 이동
                e.Graphics.TranslateTransform(cx, cy);
                // 현재 각도만큼 회전
                e.Graphics.RotateTransform(angle);
                // 이미지의 중심이 컨트롤의 중심에 오도록 다시 이동
                e.Graphics.TranslateTransform(-cachedWheelImage.Width / 2f, -cachedWheelImage.Height / 2f);

                // 회전된 상태로 회전판 이미지를 그림
                e.Graphics.DrawImage(cachedWheelImage, 0, 0, cachedWheelImage.Width, cachedWheelImage.Height);

                // 변환 초기화(다음 그리기를 위해)
                e.Graphics.ResetTransform();
            }
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
            catch (Exception ex) { LogWrite("[ROTATEIMAGE] " + ex); return null; }
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

                    using (System.Drawing.Font font = new System.Drawing.Font("맑은 고딕", fontSize, FontStyle.Bold))
                    using (Brush brush = new SolidBrush(textColor))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(text, font, brush, pbSpin.ClientRectangle, sf);
                        text = null;
                        font.Dispose();
                        brush.Dispose();
                        sf.Dispose();
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
                        using (System.Drawing.Font testFont = new System.Drawing.Font("맑은 고딕", fontSize, FontStyle.Bold))
                        {
                            textSize = gTest.MeasureString(winnerName, testFont);
                        }
                        while (textSize.Width > pbSpin.Width * 0.9f && fontSize > minFontSize)
                        {
                            fontSize -= 1f; // 텍스트가 버튼을 넘지 않도록 폰트 크기 줄임
                            using (System.Drawing.Font testFont = new System.Drawing.Font("맑은 고딕", fontSize, FontStyle.Bold))
                            {
                                textSize = gTest.MeasureString(winnerName, testFont);
                            }
                        }
                    }

                    using (System.Drawing.Font font = new System.Drawing.Font("맑은 고딕", fontSize, FontStyle.Bold))
                    using (Brush brush = new SolidBrush(Color.Blue))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(winnerName, font, brush, pbSpin.ClientRectangle, sf);
                        winnerName = null;
                        font.Dispose();
                        brush.Dispose();
                        sf.Dispose();
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
                    using (System.Drawing.Font font = new System.Drawing.Font("맑은 고딕", fontSize, FontStyle.Bold))
                    using (Brush brush = new SolidBrush(Color.Gray))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(text, font, brush, pbSpin.ClientRectangle, sf);
                        text = null;
                        font.Dispose();
                        brush.Dispose();
                        sf.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                LogWrite("[REMAINSECONDS] " + ex);
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
                LogWrite("[GETCURRENTSELECTEDNAME] " + ex);
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
                    catch (Exception ex) { LogWrite("[PROCESSROULETTERESULT-MEMBERROW] " + ex); }
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
                    catch (Exception ex) { LogWrite("[PROCESSROULETTERESULT-GIFTROW] " + ex); }
                }
                await SaveGiftsToCsv();
            }
            catch (Exception ex) { LogWrite("[PROCESSROULETTERESULT] " + ex); }
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
                    catch (Exception ex) { LogWrite("[SAVEMEMBERSCSV-ROW] " + ex); }
                }

                try
                {
                    await Task.Run(() =>
                    {
                        lock (memberCsvLock)
                        {
                            File.WriteAllLines("members.csv", lines);
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogWrite("[SAVEMEMBERSCSV-WRITE] " + ex);
                    if (SynchronizationContext.Current == null)
                        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                    await Task.Factory.StartNew(() =>
                    {
                        MessageBox.Show("CSV 파일을 쓰는 중 오류가 발생했습니다.\n실행중에는 CSV 파일을 열지 마세요.:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
            catch (Exception ex) { LogWrite("[SAVEMEMBERSCSV] " + ex); }
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
                    catch (Exception ex) { LogWrite("[SAVEGIFTSCSV-ROW] " + ex); }
                }

                try
                {
                    await Task.Run(() =>
                    {
                        lock (giftCsvLock)
                        {
                            File.WriteAllLines("gifts.csv", lines);
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogWrite("[SAVEGIFTSCSV-WRITE] " + ex);
                    if (SynchronizationContext.Current == null)
                        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                    await Task.Factory.StartNew(() =>
                    {
                        MessageBox.Show("CSV 파일을 쓰는 중 오류가 발생했습니다.\n실행중에는 CSV 파일을 열지 마세요.:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
            catch (Exception ex) { LogWrite("[SAVEGIFTSCSV] " + ex); }
        }

        // 폼 크기 변경 시 회전판/버튼 재배치
        private void Roulette_Resize(object sender, EventArgs e)
        {
            try { RedrawWheel(); } catch (Exception ex) { LogWrite("[RESIZE] " + ex); }
        }

        // SPIN 버튼에 커서를 올리면 이미지 변경
        private void pbSpin_MouseHover(object sender, EventArgs e)
        {
            try // [SPINHOVER]
            {
                if (!spinning)
                {
                    winnerName = null;
                    pbSpin.BackgroundImage = null;
                    pbSpin.BackgroundImage = btnSpin2Image;
                }
            }
            catch (Exception ex) { LogWrite("[SPINHOVER] " + ex); }
        }
        // 커서가 SPIN 버튼에서 벗어나면 이미지 원래대로
        private void pbSpin_MouseLeave(object sender, EventArgs e)
        {
            try // [SPINLEAVE]
            {
                if (!spinning)
                {
                    winnerName = null;
                    pbSpin.BackgroundImage = null;
                    pbSpin.BackgroundImage = btnSpinImage;
                }
            }
            catch (Exception ex) { LogWrite("[SPINLEAVE] " + ex); }
        }

        // 멤버 추가 버튼 클릭
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
                        LogWrite("AddMember: " + name);
                    }
                    catch (Exception ex) { LogWrite("[ADDMEMBER-DGV] " + ex); MessageBox.Show("멤버 추가 중 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }

                    try { txtAddMembers.Clear(); } catch (Exception ex) { LogWrite("[ADDMEMBER-TXTCLR] " + ex); }
                    try { _ = SaveMembersToCsv(); } catch (Exception ex) { LogWrite("[ADDMEMBER-SAVECSV] " + ex); }
                    try { RedrawWheel(); } catch (Exception ex) { LogWrite("[ADDMEMBER-REDRAW] " + ex); }
                }
            }
            catch (Exception ex) { LogWrite("[ADDMEMBER] " + ex); MessageBox.Show("멤버 추가 처리 중 예기치 못한 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // 선물 추가 버튼 클릭
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
                catch (Exception ex) { LogWrite("[ADDGIFT-CHECKDUP] " + ex); }

                if (!string.IsNullOrEmpty(gift) && !exists)
                {
                    int rowIndex = -1;
                    try
                    {
                        rowIndex = dgvGifts.Rows.Add();
                        dgvGifts.Rows[rowIndex].Cells["gGiftColumn"].Value = gift;
                        dgvGifts.Rows[rowIndex].Cells["gMemberColumn"].Value = "";
                        LogWrite("AddGift: " + gift);
                    }
                    catch (Exception ex) { LogWrite("[ADDGIFT-DGV] " + ex); MessageBox.Show("선물 추가 중 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }

                    try { txtAddGifts.Clear(); } catch (Exception ex) { LogWrite("[ADDGIFT-TXTCLR] " + ex); }
                    try { _ = SaveGiftsToCsv(); } catch (Exception ex) { LogWrite("[ADDGIFT-SAVECSV] " + ex); }
                    try { RedrawWheel(); } catch (Exception ex) { LogWrite("[ADDGIFT-REDRAW] " + ex); }
                }
            }
            catch (Exception ex) { LogWrite("[ADDGIFT] " + ex); MessageBox.Show("선물 추가 처리 중 예기치 못한 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // DataGridView 편집/삭제
        private void dgvMembers_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (dgvMembers.Columns[e.ColumnIndex].Name == "mMemberColumn")
                prevMemberName = dgvMembers.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";
        }
        private void dgvGifts_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (dgvGifts.Columns[e.ColumnIndex].Name == "gGiftColumn")
                prevGiftName = dgvGifts.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";
        }
        private void dgvMembers_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (isLoading) return; // 로딩 중이면 무시

            try
            {
                // mMemberColumn(이름) 컬럼만 체크
                if (dgvMembers.Columns[e.ColumnIndex].Name == "mMemberColumn")
                {
                    string newValue = dgvMembers.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(newValue))
                    {
                        int count = 0;
                        foreach (DataGridViewRow row in dgvMembers.Rows)
                        {
                            if (row.IsNewRow) continue;
                            if ((row.Cells["mMemberColumn"].Value?.ToString() ?? "") == newValue)
                                count++;
                        }
                        if (count > 1)
                        {
                            MessageBox.Show("이미 존재하는 멤버 이름입니다.", "중복 경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            dgvMembers.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = prevMemberName; // 중복이면 이전 값으로 되돌림
                            return;
                        }
                        if (prevMemberName != newValue)
                        {
                            LogWrite($"ModifyMember: {prevMemberName} -> {newValue}");
                        }
                    }
                }
                RedrawWheel();
            }
            catch (Exception ex) { LogWrite("[MEMBERS-CELLVALUECHANGED] " + ex); }
        }
        private void dgvGifts_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (isLoading) return; // 로딩 중이면 무시

            try
            {
                // gGiftColumn(선물명) 컬럼만 체크
                if (dgvGifts.Columns[e.ColumnIndex].Name == "gGiftColumn")
                {
                    string newValue = dgvGifts.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(newValue))
                    {
                        int count = 0;
                        foreach (DataGridViewRow row in dgvGifts.Rows)
                        {
                            if (row.IsNewRow) continue;
                            if ((row.Cells["gGiftColumn"].Value?.ToString() ?? "") == newValue)
                                count++;
                        }
                        //if (count > 1)
                        //{
                        //    MessageBox.Show("이미 존재하는 선물명입니다.", "중복 경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        //    dgvGifts.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = prevGiftName; // 중복이면 이전 값으로 되돌림
                        //    return;
                        //}
                        if (prevGiftName != newValue)
                        {
                            LogWrite($"ModifyGift: {prevGiftName} -> {newValue}");
                        }
                    }
                }
                RedrawWheel();
            }
            catch (Exception ex) { LogWrite("[GIFTS-CELLVALUECHANGED] " + ex); }
        }
        private void dgvMembers_PasteAndDelete(object sender, KeyEventArgs e)
        {
            // 멤버 붙여넣기
            if (e.Control && e.KeyCode == Keys.V)
            {
                try
                {
                    isLoading = true; // 로딩 시작

                    string clipboardText = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(clipboardText))
                    {
                        var lines = clipboardText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            string name = line.Trim();
                            if (!string.IsNullOrEmpty(name) && !nameList.Contains(name))
                            {
                                int rowIndex = dgvMembers.Rows.Add();
                                dgvMembers.Rows[rowIndex].Cells["mMemberColumn"].Value = name;
                                dgvMembers.Rows[rowIndex].Cells["mResultColumn"].Value = "";
                                nameList.Add(name);
                                remainingNames.Add(name);
                                LogWrite("AddMember(Paste): " + name);
                            }
                        }
                        _ = SaveMembersToCsv();
                        RedrawWheel();
                    }

                    isLoading = false; // 로딩 끝
                }
                catch (Exception ex)
                {
                    LogWrite("[PASTE-MEMBERS] " + ex);
                    MessageBox.Show("붙여넣기 중 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                e.Handled = true;
            }

            // 멤버 삭제
            if (e.KeyCode == Keys.Delete)
            {
                // 선택된 행 인덱스 중복 없이 수집
                var rowIndexes = dgvMembers.SelectedCells
                    .Cast<DataGridViewCell>()
                    .Select(cell => cell.RowIndex)
                    .Distinct()
                    .OrderByDescending(i => i) // 역순 삭제
                    .ToList();

                if (rowIndexes.Count > 0)
                {
                    var result1 = MessageBox.Show("정말 삭제하시겠습니까?", "삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result1 == DialogResult.Yes)
                    {
                        var result2 = MessageBox.Show("정말로 삭제할까요? 이 작업은 되돌릴 수 없습니다.", "최종 삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (result2 == DialogResult.Yes)
                        {
                            foreach (var rowIndex in rowIndexes)
                            {
                                if (!dgvMembers.Rows[rowIndex].IsNewRow)
                                {
                                    string name = dgvMembers.Rows[rowIndex].Cells["mMemberColumn"].Value?.ToString() ?? "";
                                    //nameList.Remove(name);
                                    //remainingNames.Remove(name);
                                    //selectedNames.Remove(name);
                                    nameList.RemoveAll(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
                                    remainingNames.RemoveAll(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
                                    selectedNames.RemoveAll(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
                                    LogWrite("DeleteMember: " + name);
                                    dgvMembers.Rows.RemoveAt(rowIndex);
                                }
                            }
                            _ = SaveMembersToCsv();
                            RedrawWheel();
                        }
                    }
                    e.Handled = true;
                }
            }
        }
        private void dgvGifts_PasteAndDelete(object sender, KeyEventArgs e)
        {
            // 선물 붙여넣기
            if (e.Control && e.KeyCode == Keys.V)
            {
                try
                {
                    isLoading = true; // 로딩 시작

                    string clipboardText = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(clipboardText))
                    {
                        var lines = clipboardText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            string gift = line.Trim();
                            if (!string.IsNullOrEmpty(gift))
                            {
                                int rowIndex = dgvGifts.Rows.Add();
                                dgvGifts.Rows[rowIndex].Cells["gGiftColumn"].Value = gift;
                                dgvGifts.Rows[rowIndex].Cells["gMemberColumn"].Value = "";
                                LogWrite("AddGift(Paste): " + gift);
                            }
                        }
                        _ = SaveGiftsToCsv();
                        RedrawWheel();
                    }

                    isLoading = false; // 로딩 끝
                }
                catch (Exception ex)
                {
                    LogWrite("[PASTE-GIFTS] " + ex);
                    MessageBox.Show("붙여넣기 중 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                e.Handled = true;
            }

            // 선물 삭제
            if (e.KeyCode == Keys.Delete)
            {
                var rowIndexes = dgvGifts.SelectedCells
                    .Cast<DataGridViewCell>()
                    .Select(cell => cell.RowIndex)
                    .Distinct()
                    .OrderByDescending(i => i)
                    .ToList();

                if (rowIndexes.Count > 0)
                {
                    var result1 = MessageBox.Show("정말 삭제하시겠습니까?", "삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result1 == DialogResult.Yes)
                    {
                        var result2 = MessageBox.Show("정말로 삭제할까요? 이 작업은 되돌릴 수 없습니다.", "최종 삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (result2 == DialogResult.Yes)
                        {
                            foreach (var rowIndex in rowIndexes)
                            {
                                if (!dgvGifts.Rows[rowIndex].IsNewRow)
                                {
                                    string gift = dgvGifts.Rows[rowIndex].Cells["gGiftColumn"].Value?.ToString() ?? "";
                                    LogWrite("DeleteGift: " + gift);
                                    dgvGifts.Rows.RemoveAt(rowIndex);
                                }
                            }
                            _ = SaveGiftsToCsv();
                            RedrawWheel();
                        }
                    }
                    e.Handled = true;
                }
            }
        }

        // 로그 기록
        private void LogWrite(string message)
        {
            try
            {
                logQueue.Add($"{DateTime.Now}: {message}");
            }
            catch { /* 큐가 닫혔을 때 예외 무시 */ }
        }
        private void LogWorker(CancellationToken token)
        {
            try
            {
                foreach (var log in logQueue.GetConsumingEnumerable(token))
                {
                    try
                    {
                        lock (logLock)
                        {
                            File.AppendAllText("Roulette.log", "\n" + log + "\n");
                        }
                    }
                    catch { /* 파일 기록 실패시 무시 */ }
                }
            }
            catch (OperationCanceledException) { /* 종료 시 정상 */ }
        }

        // 프로그램 종료
        private void btnExit_Click(object sender, EventArgs e)
        {
            Roulette_FormClosing(sender, new FormClosingEventArgs(CloseReason.UserClosing, false)); // 종료 확인 메시지 호출
        }
        private void Roulette_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 종료 확인 메시지
            var result = MessageBox.Show(
                "프로그램을 종료하시겠습니까?",
                "종료 확인",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                e.Cancel = true; // 종료 취소
                return;
            }

            LogWrite("[Application Close] ");
            logCts.Cancel();
            logQueue.CompleteAdding();
            try { logTask?.Wait(1000); } catch { }
        }
    }
}