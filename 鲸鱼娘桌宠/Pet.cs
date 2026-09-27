// 鲸鱼娘桌宠 v3 —— 物理 / 多形态 / 逐像素 alpha / 饿晕 / 抓边 / 多样投喂 / 摸头
//
// 渲染：UpdateLayeredWindow + 32bpp ARGB 离屏位图（每帧整幅推送）。
//       不用 TransparencyKey —— 它只剔除"颜色恰好等于键色"的像素，抗锯齿边缘会留混色描边，
//       而且物理每帧移动窗口时会闪。分层窗口没有这两个问题，alpha=0 的像素还能让点击穿透。
//
// 抓取：按下时记录"抓点相对脚底中心"的向量，之后每步都用它把抓点钉回光标下。
//       全程用 Cursor.Position（屏幕坐标），不用 MouseEventArgs 坐标（事件会滞后），
//       所以拖拽不漂。--dragetest 会把每一步偏差打出来。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WhalePet
{
    // ============================================================ 文案
    internal sealed class Strings
    {
        public string title = "鲸鱼娘";
        public string menuFeed = "投喂白米饭";
        public string menuPoke = "戳一下";
        public string menuPat = "摸摸头";
        public string menuFood = "换一种吃的";
        public string menuForm = "换个样子";
        public string menuTop = "窗口置顶";
        public string menuReset = "回到初始位置";
        public string menuExit = "让她去睡觉（退出）";
        public string trayFeed = "投喂米饭";
        public string trayShow = "显示 / 隐藏";
        public string label = "米饭";
        public string formQ = "Q版";
        public string formTall = "长大版";
        public string credit = "";
        public string foodRice = "白米饭", foodFish = "小鱼干", foodBun = "小笼包", foodCola = "汽水";

        public string[] onFeed = new string[0];
        public string[] onFeedFish = new string[0];
        public string[] onFeedBun = new string[0];
        public string[] onFeedCola = new string[0];
        public string[] onDrag = new string[0];
        public string[] onPoke = new string[0];
        public string[] onPat = new string[0];
        public string[] onPatMany = new string[0];
        public string[] onHungry = new string[0];
        public string[] onStarve = new string[0];
        public string[] onWake = new string[0];
        public string[] onIdle = new string[0];
        public string[] onThrow = new string[0];
        public string[] onLand = new string[0];
        public string[] onGrabEdge = new string[0];
        public string[] onGrow = new string[0];
        public string[] onShrink = new string[0];

        private static string Str(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Unescape(m.Groups[1].Value) : null;
        }

        private static string Unescape(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    if (n == 'n') sb.Append('\n');
                    else if (n == 't') sb.Append('\t');
                    else if (n == 'u' && i + 4 < s.Length) { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else sb.Append(n);
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private static string[] Arr(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            if (!m.Success) return new string[0];
            var list = new List<string>();
            foreach (Match q in Regex.Matches(m.Groups[1].Value, "\"((?:[^\"\\\\]|\\\\.)*)\""))
                list.Add(Unescape(q.Groups[1].Value));
            return list.ToArray();
        }

        public static Strings Load(string path)
        {
            var s = new Strings();
            if (!File.Exists(path)) return s;
            string j = File.ReadAllText(path, Encoding.UTF8);
            string v;
            if ((v = Str(j, "title")) != null) s.title = v;
            if ((v = Str(j, "menuFeed")) != null) s.menuFeed = v;
            if ((v = Str(j, "menuPoke")) != null) s.menuPoke = v;
            if ((v = Str(j, "menuPat")) != null) s.menuPat = v;
            if ((v = Str(j, "menuFood")) != null) s.menuFood = v;
            if ((v = Str(j, "menuForm")) != null) s.menuForm = v;
            if ((v = Str(j, "menuTop")) != null) s.menuTop = v;
            if ((v = Str(j, "menuReset")) != null) s.menuReset = v;
            if ((v = Str(j, "menuExit")) != null) s.menuExit = v;
            if ((v = Str(j, "trayFeed")) != null) s.trayFeed = v;
            if ((v = Str(j, "trayShow")) != null) s.trayShow = v;
            if ((v = Str(j, "label")) != null) s.label = v;
            if ((v = Str(j, "formQ")) != null) s.formQ = v;
            if ((v = Str(j, "formTall")) != null) s.formTall = v;
            if ((v = Str(j, "credit")) != null) s.credit = v;
            if ((v = Str(j, "foodRice")) != null) s.foodRice = v;
            if ((v = Str(j, "foodFish")) != null) s.foodFish = v;
            if ((v = Str(j, "foodBun")) != null) s.foodBun = v;
            if ((v = Str(j, "foodCola")) != null) s.foodCola = v;
            s.onFeed = Arr(j, "onFeed");
            s.onFeedFish = Arr(j, "onFeedFish");
            s.onFeedBun = Arr(j, "onFeedBun");
            s.onFeedCola = Arr(j, "onFeedCola");
            s.onDrag = Arr(j, "onDrag");
            s.onPoke = Arr(j, "onPoke");
            s.onPat = Arr(j, "onPat");
            s.onPatMany = Arr(j, "onPatMany");
            s.onHungry = Arr(j, "onHungry");
            s.onStarve = Arr(j, "onStarve");
            s.onWake = Arr(j, "onWake");
            s.onIdle = Arr(j, "onIdle");
            s.onThrow = Arr(j, "onThrow");
            s.onLand = Arr(j, "onLand");
            s.onGrabEdge = Arr(j, "onGrabEdge");
            s.onGrow = Arr(j, "onGrow");
            s.onShrink = Arr(j, "onShrink");
            return s;
        }
    }

    // ============================================================ 形态
    internal sealed class FormSkin
    {
        public string Name;
        public string FileName;
        public float Scale = 1f;
        public Bitmap Sprite, SpriteBlush, SpriteSleep;
        public int FeetY, WinW, WinH;
        public float EyeY, EyeDx, EyeW, HeadTop;
        public bool IsAdult;
        public float HeadRatio;
        public float Stretch = 1f;

        public static FormSkin Build(string assetsDir, string file, int targetW, string name,
                                   float eyeY, float eyeDx, float eyeW)
        {
            string p = Path.Combine(assetsDir, file);
            if (!File.Exists(p)) return null;
            Bitmap raw = LoadArgb(p);
            if (raw == null) return null;
            var skin = Wrap(raw, targetW, name, file, eyeY, eyeDx, eyeW);
            raw.Dispose();
            return skin;
        }

        internal static Bitmap LoadArgb(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var tmp = new Bitmap(fs))
                {
                    var b = new Bitmap(tmp.Width, tmp.Height, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(b))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(tmp, 0, 0, tmp.Width, tmp.Height);
                    }
                    return b;
                }
            }
            catch { return null; }
        }

        /// 几何法生成成年形态：量出头部占比（最窄处 = 颈），把头以下纵向拉伸到目标头身比。
        /// targetW 是拉伸后的宽度；高度按比例自适应，所以结果是"变高"而不是"变小"。
        /// 只改比例，不改角色设计 —— 素材许可也不允许我改完再说这是另一个角色。
        public static FormSkin BuildAdult(Bitmap reference, int targetW, string name, float targetHead, float headFraction,
                                   float eyeY, float eyeDx, float eyeW)
        {
            if (headFraction <= 0.05f) headFraction = 0.42f;
            float stretch = Math.Max(1f, Math.Min(2.6f, headFraction / Math.Max(0.10f, targetHead)));
            Bitmap raw = StretchBody(reference, stretch, headFraction);
            var skin = Wrap(raw, targetW, name, "geometry:adult", eyeY, eyeDx, eyeW);
            skin.IsAdult = true;
            skin.HeadRatio = headFraction / stretch;
            skin.Stretch = stretch;
            raw.Dispose();
            return skin;
        }

        /// 头部占比 = 最窄处(颈)到头顶 / 总高
        public static float MeasureHeadRatio(Bitmap bmp)
        {
            Rectangle box = AlphaBox(bmp);
            int n = box.Height;
            if (n < 8) return 0f;
            int[] w = new int[n];
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] buf = new byte[d.Stride * bmp.Height];
                Marshal.Copy(d.Scan0, buf, 0, buf.Length);
                for (int y = 0; y < n; y++)
                {
                    int sy = box.Top + y, lo = int.MaxValue, hi = -1;
                    for (int x = box.Left; x < box.Right; x++)
                        if (buf[sy * d.Stride + x * 4 + 3] > 40) { if (x < lo) lo = x; if (x > hi) hi = x; }
                    w[y] = hi < 0 ? 0 : hi - lo + 1;
                }
            }
            finally { bmp.UnlockBits(d); }

            int lo15 = (int)(n * 0.15), hi55 = (int)(n * 0.55);
            int neck = -1, best = int.MaxValue;
            for (int y = lo15; y <= hi55 && y < n; y++)
                if (w[y] > 4 && w[y] < best) { best = w[y]; neck = y; }
            if (neck < 0) return 0f;
            return (float)neck / n;
        }

        private static Bitmap StretchBody(Bitmap bmp, float stretch, float headFraction)
        {
            Rectangle box = AlphaBox(bmp);
            int W = box.Width, H = box.Height;
            int neck = (int)(H * Math.Max(0.10f, Math.Min(0.80f, headFraction)));
            if (neck < 4) neck = (int)(H * 0.42f);
            int outH = neck + (int)Math.Round((H - neck) * stretch);

            const int SS = 3;
            var big = new Bitmap(W * SS, outH * SS, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(big))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                int featherN = Math.Max(2, neck / 12);
                for (int oy = 0; oy < outH * SS; oy++)
                {
                    float dy = oy / (float)SS;
                    float sy;
                    if (dy <= neck) sy = dy;
                    else
                    {
                        float extra = dy - neck;
                        float sp = stretch;
                        if (featherN > 0 && extra < featherN)
                        {
                            float k = extra / featherN;
                            k = k * k * (3f - 2f * k);
                            sp = 1f + (stretch - 1f) * k;
                        }
                        sy = neck + extra / sp;
                    }
                    if (sy >= H) continue;
                    g.DrawImage(bmp, new RectangleF(0, dy, W, 1.5f),
                        new RectangleF(box.Left, box.Top + sy, W, 1f), GraphicsUnit.Pixel);
                }
            }

            var outp = new Bitmap(W, outH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(outp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(big, new Rectangle(0, 0, W, outH), new Rectangle(0, 0, W * SS, outH * SS), GraphicsUnit.Pixel);
            }
            big.Dispose();
            return outp;
        }

        private static FormSkin Wrap(Bitmap raw, int targetW, string name, string fileName,
                              float eyeYFrac, float eyeDxFrac, float eyeWFrac)
        {
            Rectangle box = AlphaBox(raw);
            if (targetW < 60) targetW = 60;
            int targetH = Math.Max(60, (int)Math.Round(box.Height * (double)targetW / box.Width));
            // 顶部气室：在贴图上方补透明空间。
            // - 至少 46px 给顶部 HUD
            // - 再加上气泡所需高度：贴图越高、人物越大，气泡也越大，
            //   否则气泡会盖在角色脸上（成年形态就是因为贴图太高而"老是挡住自己"）。
            // 气泡总高 ≈ 文本行高 + 上下内边距 + 尾巴，贴图越大字号相对越小、
            // 所以用 66 + 0.26*贴图高 作为"气泡 + HUD"所需高度：
            // 贴图越高，留给气泡的空间越多，否则气泡会盖在脸上。
            int sky = Math.Max(52, (int)Math.Round(66f + targetH * 0.26f));
            var skin = new FormSkin { Name = name, FileName = fileName, Scale = targetW / 196f };
            skin.Sprite = Compose(raw, box, targetW, targetH, sky, out skin.FeetY, out skin.HeadTop);
            skin.SpriteBlush = Tint(skin.Sprite, 1.07f, 1f, 1f, 0.03f);
            skin.SpriteSleep = Tint(skin.Sprite, 0.84f, 0.87f, 1.0f, 0f, -12f);
            skin.WinW = skin.Sprite.Width;
            skin.WinH = skin.Sprite.Height;
            // 眼睛位置在"最终贴图"上实测，而不是拿原始素材的比例去套。
            // 两者坐标系不同（贴图有 10px 边距、且按目标宽度缩放过），
            // 早先按原始素材的 0.38 去画，结果闭眼线落在额头上，看着像"眉毛"。
            skin.SetEyes(eyeYFrac, eyeDxFrac, eyeWFrac);
            return skin;
        }

        /// 按比例设定眼睛位置。比例由对素材叠加 2% 网格目视量取得到（见文档），
        /// 坐标基准是"合成后的贴图"：eyeYFrac 是相对贴图高，eyeDxFrac / eyeWFrac 相对贴图宽。
        /// 试过自动检测（暗像素质心 / 蓝青虹膜 / 肤色定脸），全部失败：
        /// 头发又蓝又暗，任何一种都会把头发算进去，质心只反映暗色团块中心，与眼睛无关。
        public void SetEyes(float eyeYFrac, float eyeDxFrac, float eyeWFrac)
        {
            EyeY = Sprite.Height * eyeYFrac;
            EyeDx = Sprite.Width * eyeDxFrac;
            EyeW = Sprite.Width * eyeWFrac;
            EyeCalibrated = true;
        }

        /// 在已合成好的贴图上实测瞳孔（此路不通，仅保留作对照，不再用于定位）
        private void MeasureEyesUnused()
        {
            int w = Sprite.Width, h = Sprite.Height;
            int y0 = (int)(h * 0.34), y1 = (int)(h * 0.62);   // 脸部候选带
            long ln = 0, rn = 0; double lx = 0, rx = 0, ly = 0, ry = 0;
            int minY = int.MaxValue, maxY = 0, minX = int.MaxValue, maxX = 0;
            BitmapData d = Sprite.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] buf = new byte[d.Stride * h];
                Marshal.Copy(d.Scan0, buf, 0, buf.Length);
                for (int y = y0; y < y1; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * d.Stride + x * 4;
                        int B = buf[i], G = buf[i + 1], R = buf[i + 2], A = buf[i + 3];
                        if (A < 200) continue;
                        int lum = (R * 299 + G * 587 + B * 114) / 1000;
                        if (lum > 110) continue;                       // 瞳孔是暗的
                        int mx = Math.Max(R, Math.Max(G, B)), mn = Math.Min(R, Math.Min(G, B));
                        if (mx - mn > 60) continue;                    // 排除发色
                        if (x < w / 2) { lx += x; ly += y; ln++; } else { rx += x; ry += y; rn++; }
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                    }
            }
            finally { Sprite.UnlockBits(d); }

            if (ln > 30 && rn > 30)
            {
                double cxl = lx / ln, cxr = rx / rn, cy = (ly / ln + ry / rn) / 2.0;
                EyeDx = (float)Math.Abs(cxr - cxl) / 2f;       // 中心到单眼的水平距离
                EyeY = (float)cy;                              // 贴图内的 y（从顶往下）
                EyeW = Math.Max(10f, Math.Min((w * 0.34f), (maxX - minX) * 0.34f));
            }
            else
            {
                EyeDx = w * 0.115f;
                EyeY = h * 0.47f;
                EyeW = w * 0.13f;
            }
            EyeCalibrated = (ln > 30 && rn > 30);
            EyeSampleCount = (int)(ln + rn);
        }

        public bool EyeCalibrated;
        public int EyeSampleCount;

        private static Rectangle AlphaBox(Bitmap b)
        {
            int minX = b.Width, minY = b.Height, maxX = -1, maxY = -1;
            BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] buf = new byte[d.Stride * b.Height];
                Marshal.Copy(d.Scan0, buf, 0, buf.Length);
                for (int y = 0; y < b.Height; y++)
                    for (int x = 0; x < b.Width; x++)
                    {
                        if (buf[y * d.Stride + x * 4 + 3] < 8) continue;
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            }
            finally { b.UnlockBits(d); }
            if (maxX < 0) return new Rectangle(0, 0, b.Width, b.Height);
            return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        }

        private static Bitmap Compose(Bitmap raw, Rectangle box, int w, int h, int sky, out int feetY, out float headTop)
        {
            const int margin = 10;
            if (sky < 0) sky = 0;
            var body = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(body))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(raw, new Rectangle(0, 0, w, h), box, GraphicsUnit.Pixel);
            }

            var canvas = new Bitmap(w + margin * 2, h + margin * 2 + sky, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(canvas))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingMode = CompositingMode.SourceOver;
                int shW = (int)(w * 0.52), shH = Math.Max(8, (int)(h * 0.045));
                using (var sh = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                    for (int i = 3; i >= 1; i--)
                        g.FillEllipse(sh, margin + w / 2 - shW / 2 - i, margin + sky + h - shH - i + 2, shW + i * 2, shH + i * 2);

                using (var ia = new ImageAttributes())
                {
                    ia.SetColorMatrix(new ColorMatrix(new[]
                    {
                        new float[]{0,0,0,0,0}, new float[]{0,0,0,0,0}, new float[]{0,0,0,0,0},
                        new float[]{0,0,0,1,0}, new float[]{1,1,1,0,1}
                    }));
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dy = -2; dy <= 2; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (dx * dx + dy * dy > 5) continue;
                            g.DrawImage(body, new Rectangle(margin + dx, margin + sky + dy, w, h),
                                0, 0, w, h, GraphicsUnit.Pixel, ia);
                        }
                }
                g.DrawImage(body, margin, margin + sky, w, h);
            }
            body.Dispose();
            feetY = margin + sky + h;
            headTop = margin + sky;
            return canvas;
        }

        private static Bitmap Tint(Bitmap src, float gainR, float gainG, float gainB, float bright, float lumShift = 0f)
        {
            var outp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(outp))
            using (var ia = new ImageAttributes())
            {
                float l = lumShift / 255f;
                ia.SetColorMatrix(new ColorMatrix(new[]
                {
                    new float[]{gainR,0,0,0,0}, new float[]{0,gainG,0,0,0}, new float[]{0,0,gainB,0,0},
                    new float[]{0,0,0,1,0}, new float[]{bright,l,l,0,1}
                }));
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height),
                    0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
            return outp;
        }
    }

    // ============================================================ 粒子
    internal sealed class Particle
    {
        public float X, Y, VX, VY, Life, Max, Size;
        public int Kind;                       // 0米粒 1心 2星 3尘 4鱼 5包子 6气泡 7抚点
        public int R = 255, G = 252, B = 238;
    }

    // ============================================================ 菜单配色
    // 自己画菜单项，而不是靠系统主题推导文字颜色。
    // 旧写法只给了 ProfessionalColorTable，子菜单项仍按系统浅色主题推导前景色，
    // 结果深底配深字（"详细的选项还是黑的"）。这里前景色由代码写死。
    internal sealed class DarkMenuRenderer : ToolStripRenderer
    {
        private static readonly Color Bg = Color.FromArgb(20, 24, 38);
        private static readonly Color Fg = Color.FromArgb(235, 240, 255);
        private static readonly Color FgDim = Color.FromArgb(146, 166, 210);
        private static readonly Color Hot = Color.FromArgb(52, 68, 112);
        private static readonly Color Edge = Color.FromArgb(96, 116, 168);
        private static readonly Color Sep = Color.FromArgb(52, 64, 100);
        private static readonly Color Head = Color.FromArgb(150, 180, 235);

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var b = new SolidBrush(Bg)) e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(Edge, 1f))
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { /* 不要左侧色条 */ }

        private static bool IsHeader(ToolStripItem it)
        {
            return !it.Enabled && it.Text != null && it.Text.IndexOf('\u00b7') >= 0;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            if (!item.Selected || !item.Enabled) return;
            var r = new Rectangle(Point.Empty, item.Size);
            using (var b = new SolidBrush(Hot)) e.Graphics.FillRectangle(b, r);
            using (var pen = new Pen(Edge, 1f)) e.Graphics.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var item = e.Item;
            if (!item.Enabled)
                e.TextColor = IsHeader(item) ? Head : FgDim;
            else if (item.Selected)
                e.TextColor = Color.White;
            else
                e.TextColor = Fg;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var pen = new Pen(Sep, 1f)) e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // 勾选标记：画一个小方块 + 对勾，颜色与文字一致
            var g = e.Graphics;
            var r = e.ImageRectangle;
            int s = 12;
            int x = r.X + (r.Width - s) / 2, y = r.Y + (r.Height - s) / 2;
            using (var b = new SolidBrush(Head)) g.FillRectangle(b, x, y, s, s);
            using (var pen = new Pen(Bg, 2f))
            {
                g.DrawLine(pen, x + 2, y + 6, x + 5, y + 9);
                g.DrawLine(pen, x + 5, y + 9, x + 10, y + 3);
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            var g = e.Graphics;
            var r = e.ArrowRectangle;
            var c = new Point(r.Left + r.Width / 2 - 2, r.Top + r.Height / 2 - 2);
            using (var b = new SolidBrush(e.Item.Selected ? Color.White : Fg))
                g.FillPolygon(b, new[]
                {
                    new Point(c.X, c.Y), new Point(c.X + 5, c.Y), new Point(c.X + 2, c.Y + 5)
                });
        }
    }

    // ============================================================ 主窗体
    internal sealed class PetForm : Form
    {
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        private const int ULW_ALPHA = 2;
        private const byte AC_SRC_OVER = 0, AC_SRC_ALPHA = 1;

        private readonly Settings _cfg;
        private readonly Strings _s;
        private readonly List<FormSkin> _skins = new List<FormSkin>();
        private int _skin;

        private readonly Timer _tick = new Timer();

        private readonly Timer _hunger = new Timer();
        private readonly Timer _idleQuip = new Timer();
        private readonly Random _rnd = new Random();
        private readonly List<Particle> _parts = new List<Particle>();
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly Font _bubbleFont, _uiFont, _badgeFont;
        private readonly StringFormat _wrap = new StringFormat(StringFormatFlags.LineLimit)
        {
            Trimming = StringTrimming.None,
            FormatFlags = StringFormatFlags.LineLimit
        };

        private Bitmap _canvas;
        private Graphics _g;

        // 物理
        private float _px, _py, _vx, _vy;
        private const float GRAVITY = 2100f, RESTITUTION = 0.46f, AIR_DRAG = 0.55f, GROUND_FRICTION = 5.2f;
        private bool _grounded = true;
        private float _squash, _tilt;

        // 抓取：抓点相对"脚底中心"的偏移（世界坐标）。按下时定一次，之后只读。
        private float _gripX, _gripY;
        private bool _gripValid;

        private float _t;
        private int _food = 1;                 // 1 米饭 2 鱼 3 包子 4 汽水
        private int _rice, _patCount;
        private float _fullness = 70f, _fedGlow, _patGlow;
        private bool _dragging, _hiddenTray, _topmost = true, _sleeping;
        private string _bubble = "";
        private float _bubbleT, _bubbleMax;
        private readonly Queue<KeyValuePair<long, Point>> _trail = new Queue<KeyValuePair<long, Point>>();
        private DateTime _lastThrow;
        private float _wilt;
        private int _leapCooldown;

        public PetForm(Settings cfg, Strings s, List<FormSkin> skins, Bitmap icon)
        {
            _cfg = cfg; _s = s; _skins.AddRange(skins);
            _canvas = new Bitmap(_skins[_skin].WinW, _skins[_skin].WinH, PixelFormat.Format32bppArgb);
            _g = Graphics.FromImage(_canvas);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(_skins[_skin].WinW, _skins[_skin].WinH);
            TopMost = _topmost;
            Text = s.title;

            _bubbleFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
            _uiFont = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
            _badgeFont = new Font("Microsoft YaHei UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point);

            MouseDown += OnDown;
            MouseMove += OnMove;
            MouseUp += OnUp;
            MouseDoubleClick += (a, b) => CycleSkin();

            _menu = BuildMenu();
            _tray = new NotifyIcon();
            IntPtr hIcon = icon.GetHicon();
            try { using (var ti = Icon.FromHandle(hIcon)) _tray.Icon = (Icon)ti.Clone(); }
            finally { DestroyIcon(hIcon); }
            _tray.Text = s.title;
            _tray.Visible = true;
            _tray.ContextMenuStrip = BuildTrayMenu();
            _tray.DoubleClick += (a, b) => { Show(); BringToFront(); };

            _tick.Interval = 16;
            _tick.Tick += (a, b) => Step(0.016f);

            _hunger.Interval = 4000;
            _hunger.Tick += (a, b) => Tick4s();
            _idleQuip.Interval = 9000;
            _idleQuip.Tick += (a, b) => IdleQuip();

            PlaceDefault();
            _tick.Start(); _hunger.Start(); _idleQuip.Start();
            Say(Pick(_s.onIdle), 3.2f, 1.0f);
        }

        private FormSkin Skins { get { return _skins[_skin]; } }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                return cp;
            }
        }

        // ======================================================== 饥饿 / 睡眠
        private void Tick4s()
        {
            if (_sleeping) return;
            _fullness = Math.Max(0f, _fullness - (100f / _cfg.fullnessMinutes) * (4f / 60f));
            if (_fullness <= 0.01f) FallAsleep(true);
        }

        private void FallAsleep(bool starved)
        {
            _sleeping = true;
            _vx = 0; _vy = 0; _grounded = true;
            if (starved) Say(Pick(_s.onStarve), 4.0f, 1.0f);
        }

        private void WakeUp()
        {
            if (!_sleeping) return;
            _sleeping = false;
            _squash = -0.10f;
            _vy -= 260f;
            _grounded = false;
            Say(Pick(_s.onWake), 2.8f, 1.0f);
        }

        // ======================================================== 菜单
        private ContextMenuStrip NewMenu()
        {
            var m = new ContextMenuStrip();
            m.Renderer = new DarkMenuRenderer();
            m.BackColor = Color.FromArgb(20, 24, 38);
            m.ForeColor = Color.FromArgb(235, 240, 255);
            m.ShowImageMargin = false;
            m.Font = new Font("Microsoft YaHei UI", 9.5f);
            return m;
        }

        private ContextMenuStrip BuildMenu()
        {
            var m = NewMenu();
            var head = new ToolStripMenuItem(_s.title + "  ·  " + Skins.Name) { Enabled = false };
            head.ForeColor = Color.FromArgb(128, 160, 210);
            m.Items.Add(head);
            m.Items.Add(new ToolStripSeparator());

            var feed = new ToolStripMenuItem(_s.menuFeed + "（" + FoodName(_food) + "）");
            feed.Click += (a, b) => Feed();
            m.Items.Add(feed);

            var food = new ToolStripMenuItem(_s.menuFood);
            for (int i = 1; i <= 4; i++)
            {
                int id = i;
                var it = new ToolStripMenuItem(FoodName(id)) { Checked = id == _food, CheckOnClick = false };
                it.Click += (a, b) => { _food = id; RebuildMenus(); Say("换成" + FoodName(id) + "了。", 2200f, 0.95f); };
                food.DropDownItems.Add(it);
            }
            m.Items.Add(food);

            var pat = new ToolStripMenuItem(_s.menuPat);
            pat.Click += (a, b) => Pat();
            m.Items.Add(pat);
            var poke = new ToolStripMenuItem(_s.menuPoke);
            poke.Click += (a, b) => Poke();
            m.Items.Add(poke);

            m.Items.Add(new ToolStripSeparator());
            var form = new ToolStripMenuItem(_s.menuForm);
            for (int i = 0; i < _skins.Count; i++)
            {
                int idx = i;
                var it = new ToolStripMenuItem(_skins[i].Name + (_skins[i].IsAdult ? "（几何）" : ""))
                { Checked = i == _skin, CheckOnClick = false };
                it.Click += (a, b) => SwitchSkin(idx, true, true);
                form.DropDownItems.Add(it);
            }
            m.Items.Add(form);

            var top = new ToolStripMenuItem(_s.menuTop) { Checked = _topmost, CheckOnClick = true };
            top.CheckedChanged += (a, b) => { _topmost = top.Checked; TopMost = _topmost; };
            m.Items.Add(top);
            var reset = new ToolStripMenuItem(_s.menuReset);
            reset.Click += (a, b) => PlaceDefault();
            m.Items.Add(reset);
            m.Items.Add(new ToolStripSeparator());
            var exit = new ToolStripMenuItem(_s.menuExit);
            exit.Click += (a, b) => Quit();
            m.Items.Add(exit);
            return m;
        }

        private void RebuildMenus()
        {
            var fresh = BuildMenu();
            _menu.Items.Clear();
            while (fresh.Items.Count > 0) _menu.Items.Add(fresh.Items[0]);
            _tray.ContextMenuStrip = BuildTrayMenu();
        }

        private ContextMenuStrip BuildTrayMenu()
        {
            var m = NewMenu();
            var feed = new ToolStripMenuItem(_s.trayFeed);
            feed.Click += (a, b) => Feed();
            m.Items.Add(feed);
            var pat = new ToolStripMenuItem(_s.menuPat);
            pat.Click += (a, b) => Pat();
            m.Items.Add(pat);
            var show = new ToolStripMenuItem(_s.trayShow);
            show.Click += (a, b) =>
            {
                if (_hiddenTray) { Show(); BringToFront(); _hiddenTray = false; }
                else { Hide(); _hiddenTray = true; }
            };
            m.Items.Add(show);
            m.Items.Add(new ToolStripSeparator());
            var exit = new ToolStripMenuItem(_s.menuExit);
            exit.Click += (a, b) => Quit();
            m.Items.Add(exit);
            return m;
        }

        private string FoodName(int id)
        {
            if (id == 2) return _s.foodFish;
            if (id == 3) return _s.foodBun;
            if (id == 4) return _s.foodCola;
            return _s.foodRice;
        }

        // ======================================================== 形态
        private void SwitchSkin(int idx, bool speak, bool preserveFeet)
        {
            if (idx < 0 || idx >= _skins.Count || idx == _skin) return;
            bool growing = idx > _skin;
            var old = ClientSize;
            _skin = idx;
            var sk = Skins;
            _canvas.Dispose();
            _canvas = new Bitmap(sk.WinW, sk.WinH, PixelFormat.Format32bppArgb);
            _g.Dispose();
            _g = Graphics.FromImage(_canvas);
            Size = new Size(sk.WinW, sk.WinH);
            if (preserveFeet)
            {
                _px -= (sk.WinW - old.Width) / 2f;
                _py -= (sk.WinH - old.Height);
            }
            _gripValid = false;
            ClampToScreens();
            if (speak) Say(Pick(growing ? _s.onGrow : _s.onShrink), 2.8f, 0.95f);
            RebuildMenus();
        }

        private void CycleSkin() { SwitchSkin((_skin + 1) % _skins.Count, true, true); }

        // ======================================================== 位置
        private void PlaceDefault()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            _px = wa.Right - 60;
            _py = wa.Bottom - 6;
            _vx = _vy = 0; _grounded = true; _gripValid = false;
            ApplyWindowPos();
        }

        private void ClampToScreens()
        {
            var wa = Screen.FromPoint(new Point((int)_px, (int)_py)).WorkingArea;
            if (_px < wa.Left + 20f) _px = wa.Left + 20f;
            if (_px > wa.Right - 20f) _px = wa.Right - 20f;
            if (_py > wa.Bottom) { _py = wa.Bottom; _vy = 0; _grounded = true; }
            if (_py < wa.Top + 40f) _py = wa.Top + 40f;
        }

        private void ApplyWindowPos()
        {
            Location = new Point((int)Math.Round(_px - Skins.WinW / 2f), (int)Math.Round(_py - Skins.FeetY));
        }

        // ======================================================== 抓取
        /// 抓点 = 光标相对"脚底中心"的偏移。之后只要 px = cursor - grip，
        /// 被抓的那一点就永远贴在光标下 —— 与窗口大小、形态无关，也不累积漂移。
        private void BeginGrip(Point cursor)
        {
            var sk = Skins;
            _gripX = (cursor.X - Location.X) - sk.WinW / 2f;
            _gripY = (cursor.Y - Location.Y) - sk.FeetY;
            _gripValid = true;
        }

        private void MoveGrip(Point cursor)
        {
            if (!_gripValid) BeginGrip(cursor);
            // 吸附整像素：_px/_py 是 float，而窗口位置必须取整。若两者各自取整，
            // 抓点会在亚像素上缓慢偏移；这里让取整只发生一次。
            _px = (float)Math.Round(cursor.X - _gripX);
            _py = (float)Math.Round(cursor.Y - _gripY);
            ApplyWindowPos();
        }

        // ======================================================== 交互
        private void OnDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_sleeping) { WakeUp(); return; }
                _dragging = true;
                _grounded = false;
                _squash = 0.10f;
                Capture = true;
                BeginGrip(Cursor.Position);
                _trail.Clear();
                _trail.Enqueue(new KeyValuePair<long, Point>(Environment.TickCount, Cursor.Position));
            }
            else if (e.Button == MouseButtons.Right)
            {
                _menu.Show(this, e.Location);
            }
        }

        private void OnMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Point cur = Cursor.Position;
            _trail.Enqueue(new KeyValuePair<long, Point>(Environment.TickCount, cur));
            while (_trail.Count > 8) _trail.Dequeue();
            MoveGrip(cur);
            _tilt = Math.Max(-16f, Math.Min(16f, _vx * 0.02f));
        }

        private void OnUp(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            Capture = false;

            long now = Environment.TickCount;
            Point old = Cursor.Position; long oldT = now;
            foreach (var kv in _trail) if (now - kv.Key <= 120) { old = kv.Value; oldT = kv.Key; break; }
            float dt = Math.Max(0.016f, (now - oldT) / 1000f);
            _vx = Math.Max(-2600f, Math.Min(2600f, (Cursor.Position.X - old.X) / dt));
            _vy = Math.Max(-2600f, Math.Min(2600f, (Cursor.Position.Y - old.Y) / dt));

            if (Math.Abs(_vx) < 240f && Math.Abs(_vy) < 240f) { Poke(); return; }
            _lastThrow = DateTime.Now;
            Say(Pick(_s.onThrow), 2.0f);
        }

        private bool IsOverBody()
        {
            Point c = Cursor.Position;
            var sk = Skins;
            float lx = c.X - Location.X, ly = c.Y - Location.Y;
            return lx > sk.WinW * 0.22f && lx < sk.WinW * 0.78f && ly > 12f && ly < sk.FeetY + 4f;
        }

        private void Poke()
        {
            if (_sleeping) { WakeUp(); return; }
            _squash = 0.18f;
            _vy -= 340f;
            _grounded = false;
            _vx += (float)(_rnd.NextDouble() * 60 - 30);
            if (_fullness < 35f) { Say(Pick(_s.onHungry), 3.0f); return; }
            Say(Pick(_s.onPoke), 2.4f);
        }

        private void Pat()
        {
            if (_sleeping) { WakeUp(); return; }
            _patCount++;
            _patGlow = 1f;
            _squash = 0.10f;
            Say(_patCount >= 3 ? Pick(_s.onPatMany) : Pick(_s.onPat), 2.6f, 0.95f);
            for (int i = 0; i < 7; i++)
                _parts.Add(new Particle
                {
                    X = _px + (float)(_rnd.NextDouble() * 60 - 30),
                    Y = _py - Skins.FeetY * 0.95f,
                    VX = (float)(_rnd.NextDouble() * 70 - 35),
                    VY = (float)(-_rnd.NextDouble() * 90 - 30),
                    Life = 0f, Max = (float)(0.8 + _rnd.NextDouble() * 0.6),
                    Size = (float)(4 + _rnd.NextDouble() * 4),
                    Kind = 7, R = 255, G = 150, B = 200
                });
        }

        private void Feed()
        {
            if (_sleeping) WakeUp();
            _rice++;
            _fullness = Math.Min(100f, _fullness + _cfg.gain(_food));
            _fedGlow = 1f;
            _vy -= 420f;
            _grounded = false;

            string[] pool = _s.onFeed;
            if (_food == 2 && _s.onFeedFish.Length > 0) pool = _s.onFeedFish;
            else if (_food == 3 && _s.onFeedBun.Length > 0) pool = _s.onFeedBun;
            else if (_food == 4 && _s.onFeedCola.Length > 0) pool = _s.onFeedCola;
            Say(Pick(pool), 3.0f);

            int kind = _food == 2 ? 4 : (_food == 3 ? 5 : (_food == 4 ? 6 : 0));
            Color col = _food == 2 ? Color.FromArgb(150, 215, 235)
                      : _food == 3 ? Color.FromArgb(245, 226, 190)
                      : _food == 4 ? Color.FromArgb(200, 235, 255)
                      : Color.FromArgb(255, 252, 238);
            for (int i = 0; i < 18; i++)
                _parts.Add(new Particle
                {
                    X = _px + (float)(_rnd.NextDouble() * 90 - 45),
                    Y = _py - Skins.FeetY * 0.55f + (float)(_rnd.NextDouble() * 24 - 12),
                    VX = (float)(_rnd.NextDouble() * 90 - 45),
                    VY = (float)(-_rnd.NextDouble() * 190 - 70),
                    Life = 0f, Max = (float)(1.0 + _rnd.NextDouble() * 1.0),
                    Size = (float)(2.4 + _rnd.NextDouble() * 2.6),
                    Kind = i % 6 == 0 ? 1 : kind, R = col.R, G = col.G, B = col.B
                });
        }

        private string Pick(string[] pool)
        {
            if (pool == null || pool.Length == 0) return "";
            return pool[_rnd.Next(pool.Length)];
        }

        private void Say(string text, float seconds, float priority = 0.5f)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_bubbleT > 0 && priority < 0.9f && seconds <= _bubbleMax) return;
            _bubble = text; _bubbleMax = seconds; _bubbleT = seconds;
        }

        private void IdleQuip()
        {
            if (_sleeping || !Visible || _dragging) return;
            if (_fullness < 30f) { Say(Pick(_s.onHungry), 3.2f, 0.9f); return; }
            if (_rnd.NextDouble() < 0.40f) Say(Pick(_s.onIdle), 2.8f);
            else if (_rnd.NextDouble() < 0.5f) LeapTowardCursor();
        }

        private void LeapTowardCursor()
        {
            if (_dragging || !_grounded || _leapCooldown > 0) return;
            Point m = Cursor.Position;
            float dx = m.X - _px;
            if (Math.Abs(dx) < 90f || Math.Abs(dx) > 780f) return;
            _vx = Math.Max(-260f, Math.Min(260f, dx * 1.1f));
            _vy = -620f;
            _grounded = false;
            _leapCooldown = 3;
        }

        private void Quit()
        {
            _tick.Stop(); _hunger.Stop(); _idleQuip.Stop();
            _tray.Visible = false;
            Application.Exit();
        }

        // ======================================================== 物理
        private void Step(float dt)
        {
            _t += dt;
            var sk = Skins;

            if (_dragging)
            {
                // 位置由 MoveGrip 驱动，这里不动
            }
            else
            {
                var wa = Screen.FromPoint(new Point((int)_px, (int)_py)).WorkingArea;
                float groundY = wa.Bottom - 6f;

                _vy += GRAVITY * dt;
                float damp = Math.Max(0f, 1f - AIR_DRAG * dt);
                _vx *= damp; _vy *= damp;
                _px += _vx * dt;
                _py += _vy * dt;

                bool wasAir = !_grounded;
                float minX = wa.Left + 20f, maxX = wa.Right - 20f;

                if (_px < minX) { _px = minX; _vx = -_vx * 0.55f; _squash = 0.12f; }
                else if (_px > maxX) { _px = maxX; _vx = -_vx * 0.55f; _squash = 0.12f; }

                if (_py >= groundY)
                {
                    float impact = _vy;
                    _py = groundY;
                    if (_vy > 60f)
                    {
                        _vy = -_vy * RESTITUTION;
                        _squash = Math.Min(0.42f, 0.06f + Math.Abs(impact) / 5200f);
                        SpawnDust(Math.Min(10, 3 + (int)(Math.Abs(impact) / 260f)));
                        if (wasAir && Math.Abs(impact) > 420f && (DateTime.Now - _lastThrow).TotalSeconds < 3.5)
                        {
                            Say(Pick(_s.onLand), 1.8f);
                            _lastThrow = DateTime.MinValue;
                        }
                    }
                    else
                    {
                        _vy = 0;
                        if (!_grounded) _squash = Math.Max(_squash, 0.10f);
                        _grounded = true;
                    }
                    _vx *= Math.Max(0f, 1f - GROUND_FRICTION * dt);
                    if (Math.Abs(_vx) < 14f) _vx = 0;
                }
                else _grounded = false;
            }

            if (float.IsNaN(_px) || float.IsNaN(_py) || Math.Abs(_px) > 40000 || Math.Abs(_py) > 40000) PlaceDefault();

            _squash *= Math.Max(0f, 1f - 7.5f * dt);
            if (!_dragging) _tilt *= Math.Max(0f, 1f - 3.2f * dt);
            _fedGlow = Math.Max(0f, _fedGlow - dt * 0.5f);
            _patGlow = Math.Max(0f, _patGlow - dt * 0.7f);
            if (_bubbleT > 0) { _bubbleT -= dt; if (_bubbleT <= 0) _bubble = ""; }
            _wilt = _fullness < 30f ? Math.Min(1f, _wilt + dt * 0.6f) : Math.Max(0f, _wilt - dt * 0.8f);
            if (_leapCooldown > 0 && _grounded) _leapCooldown--;

            for (int i = _parts.Count - 1; i >= 0; i--)
            {
                Particle p = _parts[i];
                p.Life += dt;
                if (p.Life >= p.Max) { _parts.RemoveAt(i); continue; }
                p.X += p.VX * dt; p.Y += p.VY * dt;
                p.VY += (p.Kind == 3 ? 40f : (p.Kind == 6 ? -30f : 320f)) * dt;
                p.VX *= 0.99f;
            }

            if (!_dragging) Cursor = (_sleeping || !IsOverBody()) ? Cursors.Default : Cursors.Hand;

            ApplyWindowPos();
            Render();
            Push();
        }

        private void SpawnDust(int n)
        {
            for (int i = 0; i < n; i++)
                _parts.Add(new Particle
                {
                    X = _px + (float)(_rnd.NextDouble() * 44 - 22),
                    Y = _py - 6f,
                    VX = (float)(_rnd.NextDouble() * 120 - 60),
                    VY = (float)(-_rnd.NextDouble() * 70 - 20),
                    Life = 0f, Max = (float)(0.35 + _rnd.NextDouble() * 0.4),
                    Size = (float)(3 + _rnd.NextDouble() * 4),
                    Kind = 3, R = 190, G = 200, B = 220
                });
        }

        // ======================================================== 绘制
        private void Render()
        {
            var sk = Skins;
            Graphics g = _g;
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float originX = _px - Location.X;
            float feetY = _py - Location.Y;

            foreach (Particle p in _parts)
            {
                float k = 1f - p.Life / p.Max;
                int a = (int)(215 * k);
                float x = p.X - Location.X, y = p.Y - Location.Y;
                Color col = Color.FromArgb(a, p.R, p.G, p.B);
                if (p.Kind == 1)
                    using (var b = new SolidBrush(Color.FromArgb(a, 255, 128, 170)))
                        g.FillEllipse(b, x, y, p.Size * 2.1f, p.Size * 2.1f);
                else if (p.Kind == 2)
                {
                    float s = p.Size * 1.7f;
                    using (var pen = new Pen(Color.FromArgb(a, 255, 226, 150), 1.6f))
                    {
                        g.DrawLine(pen, x - s, y, x + s, y);
                        g.DrawLine(pen, x, y - s, x, y + s);
                    }
                }
                else if (p.Kind == 3)
                    using (var b = new SolidBrush(Color.FromArgb((int)(a * 0.5f), col)))
                        g.FillEllipse(b, x, y, p.Size, p.Size * 0.8f);
                else if (p.Kind == 4)
                    using (var b = new SolidBrush(col))
                    {
                        g.FillEllipse(b, x, y, p.Size * 2.6f, p.Size * 1.2f);
                        g.FillPolygon(b, new[]
                        {
                            new PointF(x + p.Size * 0.3f, y + p.Size * 0.6f),
                            new PointF(x - p.Size * 1.2f, y - p.Size * 0.4f),
                            new PointF(x - p.Size * 1.2f, y + p.Size * 1.6f)
                        });
                    }
                else if (p.Kind == 5)
                {
                    using (var b = new SolidBrush(col))
                        g.FillEllipse(b, x, y, p.Size * 2.0f, p.Size * 1.7f);
                    using (var pen = new Pen(Color.FromArgb(a, 210, 180, 130), 1f))
                        g.DrawArc(pen, x, y, p.Size * 2.0f, p.Size * 1.7f, 200f, 140f);
                }
                else if (p.Kind == 6)
                    using (var pen = new Pen(Color.FromArgb((int)(a * 0.8f), col), 1.2f))
                        g.DrawEllipse(pen, x, y, p.Size * 1.6f, p.Size * 1.6f);
                else if (p.Kind == 7)
                    using (var b = new SolidBrush(col))
                        g.FillEllipse(b, x, y, p.Size, p.Size);
                else
                    using (var b = new SolidBrush(col))
                        g.FillEllipse(b, x, y, p.Size, p.Size * 1.5f);
            }

            float idleBob = _grounded && !_sleeping ? (float)Math.Sin(_t * 1.25f) * 2.2f : 0f;
            float hungryEase = _wilt * 0.5f;
            float sx = 1f + _squash;
            float sy = 1f - _squash - hungryEase * 0.04f;
            if (_bubbleT > 0) sy += (float)Math.Sin(_t * 15f) * 0.012f;

            Bitmap sprite = _sleeping ? sk.SpriteSleep : (_fedGlow > 0.02f ? sk.SpriteBlush : sk.Sprite);
            float rot = _tilt + (float)Math.Sin(_t * 0.8f) * 1.2f;
            if (_sleeping) { sx = 1.07f; sy = 0.86f; }

            var save = g.Save();
            g.TranslateTransform(originX, feetY - idleBob);
            g.RotateTransform(rot);
            g.ScaleTransform(sx, sy);
            g.TranslateTransform(-sk.WinW / 2f, -sk.FeetY);
            g.DrawImage(sprite, 0, 0, sprite.Width, sprite.Height);
            g.Restore(save);

            // 眨眼已整体移除：画在她脸上会被读成"长了两道眉毛"，很违和。

            if (_sleeping)
            {
                using (var pen = new Pen(Color.FromArgb(210, 255, 255, 255), 5.2f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    float ey = feetY - sk.FeetY + sk.EyeY - idleBob;
                    float h = sk.EyeW * 0.80f;
                    g.DrawArc(pen, originX - sk.EyeDx - sk.EyeW / 2f, ey - h / 2f, sk.EyeW, h, 200f, 140f);
                    g.DrawArc(pen, originX + sk.EyeDx - sk.EyeW / 2f, ey - h / 2f, sk.EyeW, h, 200f, 140f);
                }
                DrawZzz(g, originX, feetY - sk.FeetY + sk.HeadTop + 30f);
            }

            DrawHud(g, originX);
            if (!string.IsNullOrEmpty(_bubble) && _bubbleT > 0)
                DrawBubble(g, originX, feetY - sk.FeetY + sk.HeadTop - idleBob - 6f);
        }

        // 配色取自角色本身（藏青 + 米白），关键是一切背景都不透明：
        // 半透明底会让深色壁纸透上来，浅色文字立刻糊成一片。
        private static readonly Color Ink = Color.FromArgb(255, 26, 34, 66);        // 藏青
        private static readonly Color Paper = Color.FromArgb(255, 247, 249, 255);    // 米白
        private static readonly Color Edge = Color.FromArgb(255, 92, 112, 168);      // 描边

        private void DrawHud(Graphics g, float cx)
        {
            float w = 96f, h = 7f, x = cx - w / 2f, y = 10f;
            string txt = FoodName(_food) + " × " + _rice + "   [" + Skins.Name + "]";
            SizeF sz = g.MeasureString(txt, _uiFont);

            // 一块实心面板把进度条和文字都包进去，避免任何地方露出壁纸
            float padX = 9f, padY = 5f;
            float panelW = Math.Max(w, sz.Width) + padX * 2f;
            float panelH = h + sz.Height + padY * 2f + 4f;
            var panel = new RectangleF(cx - panelW / 2f, y - padY, panelW, panelH);

            using (var path = Rounded(panel, 7f))
            {
                using (var fill = new SolidBrush(Paper))
                    g.FillPath(fill, path);
                using (var pen = new Pen(Edge, 1.4f))
                    g.DrawPath(pen, path);
            }

            // 进度条
            var track = new RectangleF(cx - w / 2f, y, w, h);
            using (var fill = new SolidBrush(Color.FromArgb(255, 214, 222, 240)))
                g.FillRectangle(fill, track);
            Color c = _sleeping ? Color.FromArgb(255, 130, 142, 178)
                    : _fullness > 60 ? Color.FromArgb(255, 74, 158, 108)
                    : _fullness > 30 ? Color.FromArgb(255, 214, 158, 40)
                                     : Color.FromArgb(255, 208, 74, 66);
            using (var fill = new SolidBrush(c))
                g.FillRectangle(fill, track.X, track.Y, track.Width * (_fullness / 100f), track.Height);
            using (var pen = new Pen(Color.FromArgb(255, 120, 134, 170), 1f))
                g.DrawRectangle(pen, track.X, track.Y, track.Width, track.Height);

            // 文字（藏青实心底上的深色字，与面板对比度约 14:1）
            using (var tb = new SolidBrush(Ink))
                g.DrawString(txt, _uiFont, tb, cx - sz.Width / 2f, y + h + padY);
        }

        private void DrawBubble(Graphics g, float cx, float topY)
        {
            float maxW = Math.Min(Skins.WinW - 24f, 320f);
            if (maxW < 130f) maxW = Skins.WinW - 16f;

            // 文本可用宽度 -> 先量高，再据此定气泡尺寸。
            // 注意：MeasureString 用 width 重载时对中文换行会低估高度，会裁掉最后一行，
            // 所以这里用带 StringFormat 的重载，并额外留出余量。
            const float padX = 14f, padTop = 12f, padBottom = 12f;
            float textW = maxW - padX * 2f;
            SizeF sz = g.MeasureString(_bubble, _bubbleFont, new SizeF(textW, 1000f), _wrap);
            float w = Math.Min(maxW, sz.Width + padX * 2f + 4f);
            textW = w - padX * 2f;
            sz = g.MeasureString(_bubble, _bubbleFont, new SizeF(textW, 1000f), _wrap);
            float h = sz.Height + padTop + padBottom + 4f;

            float x = cx - w / 2f;
            if (x < 6f) x = 6f;
            if (x + w > Skins.WinW - 6f) x = Skins.WinW - 6f - w;
            float y = Math.Max(44f, topY - h - 10f);
            var rect = new RectangleF(x, y, w, h);

            var tail = new[]
            {
                new PointF(cx - 8f, rect.Bottom - 3f),
                new PointF(cx + 8f, rect.Bottom - 3f),
                new PointF(cx, rect.Bottom + 11f)
            };
            using (var tb0 = new SolidBrush(Paper))
                g.FillPolygon(tb0, tail);
            using (var pen = new Pen(Edge, 1.8f))
            {
                g.DrawLine(pen, tail[0].X, tail[0].Y, tail[2].X, tail[2].Y);
                g.DrawLine(pen, tail[1].X, tail[1].Y, tail[2].X, tail[2].Y);
            }
            using (var path = Rounded(rect, 12f))
            {
                using (var sh = new SolidBrush(Color.FromArgb(52, 0, 0, 0)))
                    g.FillPath(sh, Rounded(new RectangleF(rect.X + 1.5f, rect.Y + 2.5f, rect.Width, rect.Height), 12f));
                using (var brush = new LinearGradientBrush(rect, Paper, Color.FromArgb(255, 234, 239, 250), 90f))
                    g.FillPath(brush, path);
                using (var pen = new Pen(Edge, 1.8f))
                    g.DrawPath(pen, path);
            }
            string badge = _s.title;
            SizeF bs = g.MeasureString(badge, _badgeFont);
            var brect = new RectangleF(rect.X + 10f, rect.Y - 8f, bs.Width + 13f, bs.Height + 5f);
            using (var path = Rounded(brect, 7f))
            using (var b = new SolidBrush(Ink))
                g.FillPath(b, path);
            using (var b = new SolidBrush(Color.White))
                g.DrawString(badge, _badgeFont, b, brect.X + 6f, brect.Y + 2f);
            using (var tb = new SolidBrush(Ink))
                g.DrawString(_bubble, _bubbleFont, tb,
                    new RectangleF(rect.X + padX, rect.Y + padTop, rect.Width - padX * 2f, rect.Height - padTop - padBottom),
                    _wrap);
        }

        private void DrawZzz(Graphics g, float cx, float y)
        {
            using (var f = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (var dark = new SolidBrush(Ink))
            using (var halo = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                for (int i = 0; i < 3; i++)
                {
                    float ph = (float)((_t * 0.7f + i * 0.34f) % 1.0);
                    float zx = cx + 30f + i * 15f + ph * 6f;
                    float zy = y - i * 20f - ph * 16f;
                    // 先画一圈白边再画深色字：无论壁纸深浅都能看清
                    foreach (var off in new[] { new PointF(-1.4f, 0), new PointF(1.4f, 0), new PointF(0, -1.4f), new PointF(0, 1.4f) })
                        g.DrawString("z", f, halo, zx + off.X, zy + off.Y);
                    g.DrawString("z", f, dark, zx, zy);
                }
        }

        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2f;
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        private void Push()
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero, oldBmp = IntPtr.Zero;
            try
            {
                hBmp = _canvas.GetHbitmap(Color.FromArgb(0));
                oldBmp = SelectObject(memDc, hBmp);
                var size = new SIZE { cx = _canvas.Width, cy = _canvas.Height };
                var src = new POINT { X = 0, Y = 0 };
                var dst = new POINT { X = Location.X, Y = Location.Y };
                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER, BlendFlags = 0,
                    SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA
                };
                UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                if (hBmp != IntPtr.Zero) { SelectObject(memDc, oldBmp); DeleteObject(hBmp); }
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
                if (_canvas != null) _canvas.Dispose();
                if (_g != null) _g.Dispose();
                foreach (var s in _skins)
                {
                    if (s.Sprite != null) s.Sprite.Dispose();
                    if (s.SpriteBlush != null) s.SpriteBlush.Dispose();
                    if (s.SpriteSleep != null) s.SpriteSleep.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        // ======================================================== 自检钩子
        internal void TestFeed() { Feed(); }
        internal void TestPat() { Pat(); }
        internal void TestStep() { Step(0.016f); }

        internal void TestThrow(float vx, float vy) { _vx = vx; _vy = vy; _grounded = false; }
        internal void TestMoveTo(float x, float y) { _px = x; _py = y; ApplyWindowPos(); }
        internal void TestStarve() { FallAsleep(true); }
        internal void SwitchSkinForTest(int i) { SwitchSkin(i, false, true); }
        internal void TestShow() { Show(); }
        internal void TestPump() { Application.DoEvents(); }
        internal void TestGrab(Point p) { _dragging = true; _grounded = false; BeginGrip(p); }
        internal void TestDragTo(Point p) { MoveGrip(p); }
        internal void TestEndDrag() { _dragging = false; }
        internal float GripX { get { return _gripX; } }
        internal float GripY { get { return _gripY; } }
        internal bool IsGripValid { get { return _gripValid; } }
        internal Point TestLocation { get { return Location; } }
        internal Size TestSize { get { return Size; } }
        internal Size TestClient { get { return ClientSize; } }
        internal Size CanvasSize { get { return _canvas == null ? new Size(0, 0) : new Size(_canvas.Width, _canvas.Height); } }
        internal float TestPx { get { return _px; } }
        internal float TestPy { get { return _py; } }
        internal string SkinNames()
        {
            var sb = new StringBuilder();
            foreach (var s in _skins) sb.Append(s.Name).Append(s.IsAdult ? "(几何) " : " ");
            return sb.ToString().Trim();
        }
        internal string TestInfo()
        {
            return string.Format("skin={0} px={1:F0} py={2:F0} vx={3:F0} vy={4:F0} grounded={5} sleeping={6} parts={7}",
                Skins.Name, _px, _py, _vx, _vy, _grounded, _sleeping, _parts.Count);
        }
        internal void TestPaint(Graphics g) { Render(); g.DrawImage(_canvas, 0, 0); }
    }

    // ============================================================ 配置
    internal sealed class Settings
    {
        public float fullnessMinutes = 22f;
        public bool topmost = true;

        public int gain(int food)
        {
            if (food == 2) return 30;   // 小鱼干
            if (food == 3) return 16;   // 小笼包
            if (food == 4) return 8;    // 汽水
            return 22;                  // 白米饭
        }

        public static Settings Load(string path)
        {
            var s = new Settings();
            if (!File.Exists(path)) return s;
            try
            {
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("//") || line.Length == 0) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();
                    float f;
                    if (k == "fullnessminutes" &&
                        float.TryParse(v, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out f) && f > 0.5f)
                        s.fullnessMinutes = f;
                    else if (k == "topmost") s.topmost = v == "1" || v.ToLowerInvariant() == "true";
                }
            }
            catch { }
            return s;
        }
    }

    // ============================================================ 入口
    internal static class Program
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point p);

        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (a, b) =>
            {
                try
                {
                    string logDir = Path.Combine(Path.GetTempPath(), "whalepet");
                    Directory.CreateDirectory(logDir);
                    File.AppendAllText(Path.Combine(logDir, "error.log"),
                        DateTime.Now.ToString("s") + "  " + b.ExceptionObject + Environment.NewLine, Encoding.UTF8);
                }
                catch { }
            };
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string assets = Path.Combine(dir, "assets");
            bool selfTest = false, dragTest = false;
            foreach (string a in args)
            {
                if (a == "--selftest") selfTest = true;
                else if (a == "--dragetest") dragTest = true;
            }

            Settings cfg = Settings.Load(Path.Combine(dir, "pet-settings.txt"));
            Strings str = Strings.Load(Path.Combine(dir, "pet-strings.json"));
            var skins = BuildSkins(assets);
            if (skins.Count == 0)
            {
                MessageBox.Show("assets 目录里没有可用的透明底 PNG。\n把立绘放进去，或用 pet-forms.json 指定。",
                    "鲸鱼娘桌宠", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Bitmap icon = HeadIcon(skins[0].Sprite, 32);
            if (selfTest) { SelfTest(cfg, str, skins, icon, assets); return; }

            var form = new PetForm(cfg, str, skins, icon);
            if (dragTest) { DragTest(form); return; }
            Application.Run(form);
        }

        /// 抓取精度实测：真实移动光标并驱动拖拽，量"抓点"与"光标"的偏差（像素）
        private static void DragTest(PetForm form)
        {
            string outDir = Path.Combine(Path.GetTempPath(), "whalepet");
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder();
            Point saved;
            GetCursorPos(out saved);
            double worst = 0;
            try
            {
                form.TestShow();
                form.TestPump();
                System.Threading.Thread.Sleep(180);
                form.TestPump();

                var start = new Point(700, 420);
                SetCursorPos(start.X, start.Y);
                form.TestPump();
                System.Threading.Thread.Sleep(60);
                form.TestPump();
                form.TestGrab(start);

                Point loc0 = form.TestLocation;
                float expectX = start.X - (loc0.X + form.ClientSize.Width / 2f);
                float expectY = start.Y - (loc0.Y + form.ClientSize.Height);
                log.AppendLine("抓取精度实测（真实光标）");
                log.AppendLine(string.Format("  窗体 Size={0}x{1}  ClientSize={2}x{3}  画布={4}x{5}",
                    form.TestSize.Width, form.TestSize.Height,
                    form.TestClient.Width, form.TestClient.Height,
                    form.CanvasSize.Width, form.CanvasSize.Height));
                log.AppendLine(string.Format("  grip=({0:F1},{1:F1})  反查预期=({2:F1},{3:F1})  grip有效={4}",
                    form.GripX, form.GripY, expectX, expectY, form.IsGripValid));

                for (int i = 1; i <= 16; i++)
                {
                    int cx = start.X + i * 37;
                    int cy = start.Y + (i % 4) * 31 - 46;
                    SetCursorPos(cx, cy);
                    form.TestPump();
                    form.TestDragTo(new Point(cx, cy));
                    form.TestPump();

                    // 抓点应贴在光标下：世界坐标 px/py 满足 px = cursorX - gripX, py = cursorY - gripY
                    double gx = form.TestPx + form.GripX;
                    double gy = form.TestPy + form.GripY;
                    double dx = gx - cx, dy = gy - cy;
                    worst = Math.Max(worst, Math.Max(Math.Abs(dx), Math.Abs(dy)));
                    log.AppendLine(string.Format("  步{0,2} 光标({1,4},{2,4}) 抓点({3:F1},{4:F1}) 偏差 dx={5:F2} dy={6:F2}",
                        i, cx, cy, gx, gy, dx, dy));
                }
                log.AppendLine(string.Format("最大偏差 {0:F2} px -> {1}", worst, worst <= 1.0 ? "PASS" : "FAIL"));
                form.TestEndDrag();
            }
            catch (Exception ex) { log.AppendLine("异常: " + ex); }
            finally
            {
                SetCursorPos(saved.X, saved.Y);
                File.WriteAllText(Path.Combine(outDir, "dragetest.txt"), log.ToString(), Encoding.UTF8);
                Console.WriteLine(log.ToString());
                Application.Exit();
            }
        }

        private static List<FormSkin> BuildSkins(string assetsDir)
        {
            var result = new List<FormSkin>();
            var entries = new List<KeyValuePair<string, int>>();
            bool wantAdult = false;
            int adultWidth = 196;
            float headFraction = 0.42f;
            float targetHead = 0.33f;

            string cfgPath = Path.Combine(assetsDir, "pet-forms.json");
            if (File.Exists(cfgPath))
            {
                string j = File.ReadAllText(cfgPath, Encoding.UTF8);
                // 支持 "width": 196 或 "scale": 1.0（scale 按 196 基准换算）
                foreach (Match m in Regex.Matches(j, "\\{[^{}]*\\}"))
                {
                    string blk = m.Value;
                    Match fm = Regex.Match(blk, "\"file\"\\s*:\\s*\"([^\"]+)\"");
                    if (!fm.Success) continue;
                    int w = 196;
                    string title = null;
                    Match ttm = Regex.Match(blk, "\"title\"\\s*:\\s*\"([^\"]+)\"");
                    if (ttm.Success) title = ttm.Groups[1].Value;
                    Match wm = Regex.Match(blk, "\"width\"\\s*:\\s*([0-9.]+)");
                    Match sm = Regex.Match(blk, "\"scale\"\\s*:\\s*([0-9.]+)");
                    if (wm.Success)
                    {
                        float v;
                        if (float.TryParse(wm.Groups[1].Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out v)) w = (int)Math.Round(v);
                    }
                    else if (sm.Success)
                    {
                        float v;
                        if (float.TryParse(sm.Groups[1].Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0f)
                            w = (int)Math.Round(196 * v);
                    }
                    // 眼睛位置：可由配置覆盖；缺省是 Q版 素材用 2% 网格量取的比例
                    float ey = 0.547f, edx = 0.108f, ew = 0.118f;   // 半距 / 单眼宽，均为贴图宽的比例
                    Match m1 = Regex.Match(blk, "\"eyeY\"\\s*:\\s*([0-9.]+)");
                    Match m2 = Regex.Match(blk, "\"eyeDx\"\\s*:\\s*([0-9.]+)");
                    Match m3 = Regex.Match(blk, "\"eyeW\"\\s*:\\s*([0-9.]+)");
                    float tmp;
                    if (m1.Success && float.TryParse(m1.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out tmp)) ey = tmp;
                    if (m2.Success && float.TryParse(m2.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out tmp)) edx = tmp;
                    if (m3.Success && float.TryParse(m3.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out tmp)) ew = tmp;
                    entries.Add(new KeyValuePair<string, int>(
                        fm.Groups[1].Value + "\u0001" + (title ?? "") + "\u0001" + ey.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + "\u0001" + edx.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + "\u0001" + ew.ToString(System.Globalization.CultureInfo.InvariantCulture), w));
                }
                wantAdult = Regex.IsMatch(j, "\"adult\"\\s*:\\s*true");
                Match hm = Regex.Match(j, "\"headFraction\"\\s*:\\s*([0-9.]+)");
                if (hm.Success)
                {
                    float v2;
                    if (float.TryParse(hm.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out v2) && v2 > 0.05f) headFraction = v2;
                }
                Match tm = Regex.Match(j, "\"targetHead\"\\s*:\\s*([0-9.]+)");
                if (tm.Success)
                {
                    float v3;
                    if (float.TryParse(tm.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out v3) && v3 > 0.10f) targetHead = v3;
                }
                Match am = Regex.Match(j, "\"adultWidth\"\\s*:\\s*([0-9.]+)");
                if (am.Success)
                {
                    float v;
                    if (float.TryParse(am.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out v)) adultWidth = (int)Math.Round(v);
                }
            }
            if (entries.Count == 0 && Directory.Exists(assetsDir))
            {
                var names = new List<string>(Directory.GetFiles(assetsDir, "*.png"));
                names.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string f in names)
                {
                    string n = Path.GetFileName(f).ToLowerInvariant();
                    if (n.Contains("preview") || n.Contains("icon") || n.Contains("banner") || n.StartsWith("web-")) continue;
                    if (!HasAlpha(f)) continue;
                    entries.Add(new KeyValuePair<string, int>(Path.GetFileName(f), entries.Count == 0 ? 196 : 259));
                }
            }

            for (int i = 0; i < entries.Count; i++)
            {
                string[] parts = entries[i].Key.Split('\u0001');
                string file = parts[0];
                string label = parts.Length > 1 && parts[1].Length > 0
                    ? parts[1]
                    : (i == 0 ? "Q版" : (i == 1 ? "长大版" : "形态" + (i + 1)));
                float e1 = 0.547f, e2 = 0.108f, e3 = 0.118f;
                if (parts.Length >= 5)
                {
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out e1);
                    float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out e2);
                    float.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out e3);
                }
                var sk = FormSkin.Build(assetsDir, file, entries[i].Value, label, e1, e2, e3);
                if (sk != null) result.Add(sk);
            }

            if (wantAdult && result.Count > 0)
            {
                Bitmap raw = FormSkin.LoadArgb(Path.Combine(assetsDir, entries[0].Key.Split('\u0001')[0]));
                if (raw != null)
                {
                    var adult = FormSkin.BuildAdult(raw, adultWidth, "成年形态", targetHead, headFraction, 0.455f, 0.140f, 0.110f);
                    if (adult != null) result.Add(adult);
                    raw.Dispose();
                }
            }
            return result;
        }

        private static bool HasAlpha(string png)
        {
            try
            {
                byte[] b = new byte[32];
                using (var fs = new FileStream(png, FileMode.Open, FileAccess.Read)) fs.Read(b, 0, 32);
                if (b[1] != 0x50) return false;
                return b[25] == 4 || b[25] == 6;
            }
            catch { return false; }
        }

        private static Bitmap HeadIcon(Bitmap sprite, int size)
        {
            var outp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(outp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(sprite, new Rectangle(0, 0, size, size),
                    new Rectangle(sprite.Width / 4, 0, sprite.Width / 2, sprite.Height / 2), GraphicsUnit.Pixel);
            }
            return outp;
        }

        // ---------------------------------------------------------- 自检
        private static void SelfTest(Settings cfg, Strings str, List<FormSkin> skins, Bitmap icon, string assets)
        {
            string outDir = Path.Combine(Path.GetTempPath(), "whalepet");
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder();
            log.AppendLine("assets  : " + assets);
            log.AppendLine("forms   : " + skins.Count);
            foreach (var s in skins)
                log.AppendLine(string.Format("  {0,-8} {1,-28} {2}x{3} scale={4} feetY={5}{6}",
                    s.Name, s.FileName, s.WinW, s.WinH, s.Scale, s.FeetY, s.IsAdult ? "  <- 几何生成" : ""));
            foreach (var s in skins)
                log.AppendLine(string.Format("  eyes    {0,-8} EyeY={1:F1} EyeDx={2:F1} EyeW={3:F1} 标定={4} 样本={5}",
                    s.Name, s.EyeY, s.EyeDx, s.EyeW, s.EyeCalibrated, s.EyeSampleCount));
            log.AppendLine("strings : feed=" + str.onFeed.Length + " fish=" + str.onFeedFish.Length +
                           " bun=" + str.onFeedBun.Length + " cola=" + str.onFeedCola.Length +
                           " pat=" + str.onPat.Length + " starve=" + str.onStarve.Length +
                           " wake=" + str.onWake.Length + " grabEdge=" + str.onGrabEdge.Length);

            var form = new PetForm(cfg, str, skins, icon);
            var wa = Screen.PrimaryScreen.WorkingArea;

            form.TestMoveTo(500, 200);
            form.TestThrow(520f, -260f);
            float maxY = 0, minY = 99999;
            for (int i = 0; i < 90; i++) { form.TestStep(); Report(form, ref minY, ref maxY); }
            log.AppendLine("physics : 90 帧 -> " + form.TestInfo());
            log.AppendLine("physics : 竖直范围 py " + minY.ToString("F0") + " .. " + maxY.ToString("F0"));

            for (int i = 0; i < 400; i++) form.TestStep();
            log.AppendLine("settle  : 400 帧 -> " + form.TestInfo());

            form.TestMoveTo(wa.Right - 20f, wa.Top + 120f);
            form.TestThrow(900f, -60f);
            for (int i = 0; i < 60; i++) form.TestStep();
            log.AppendLine("bounce  : " + form.TestInfo() + "   （撞墙只反弹，不贴边）");

            form.TestStarve();
            for (int i = 0; i < 8; i++) form.TestStep();
            log.AppendLine("starve  : " + form.TestInfo());
            Save(form, Path.Combine(outDir, "v3-sleep.png"));
            form.TestFeed();
            for (int i = 0; i < 6; i++) form.TestStep();
            log.AppendLine("wake    : " + form.TestInfo());

            form.TestMoveTo(wa.Right - 240f, wa.Bottom - 6f);
            for (int i = 0; i < 30; i++) form.TestStep();
            Save(form, Path.Combine(outDir, "v3-idle.png"));
            form.TestPat();
            for (int i = 0; i < 5; i++) form.TestStep();
            Save(form, Path.Combine(outDir, "v3-pat.png"));
            form.TestFeed();
            for (int i = 0; i < 5; i++) form.TestStep();
            Save(form, Path.Combine(outDir, "v3-feed.png"));

            log.AppendLine("render  : v3-*.png   形态: " + form.SkinNames());

            for (int i = 0; i < skins.Count; i++)
            {
                form.SwitchSkinForTest(i);
                for (int j = 0; j < 12; j++) form.TestStep();
                Save(form, Path.Combine(outDir, "v3-form-" + i + ".png"));
            }
            form.Dispose();
            File.WriteAllText(Path.Combine(outDir, "selftest.txt"), log.ToString(), Encoding.UTF8);
            Console.WriteLine(log.ToString());
        }

        private static void Report(PetForm form, ref float minY, ref float maxY)
        {
            Match m = Regex.Match(form.TestInfo(), @"py=(-?\d+)");
            if (!m.Success) return;
            float y = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (y > maxY) maxY = y;
            if (y < minY) minY = y;
        }

        private static void Save(PetForm form, string path)
        {
            var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) form.TestPaint(g);
            bmp.Save(path, ImageFormat.Png);
            bmp.Dispose();
        }
    }
}
