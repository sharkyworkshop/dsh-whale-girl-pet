# 鲸鱼娘桌宠 · dsh-whale-girl-pet

一个 Windows 桌面宠物，以及配套的「吉祥物版」网页。

- **桌宠**：C# / .NET Framework 4.x 单文件源码编译，无框架、无运行时依赖（用系统自带的 `csc.exe`）
- **渲染**：`UpdateLayeredWindow` 逐像素 alpha 分层窗口 —— 不是透明键（`TransparencyKey`）
- **物理**：重力、落地回弹、挤压拉伸、地面摩擦、撞墙反弹、拖拽抛出
- **互动**：投喂（4 种食物）、摸摸头、戳、抛、饿晕、形态切换
- **形态**：Q版 / 成年形态，右键菜单切换

---

## 目录结构

```
├─ 鲸鱼娘桌宠/                桌宠（可独立编译运行）
│  ├─ Pet.cs                  全部源码（单文件）
│  ├─ 鲸鱼娘桌宠.exe           已编译产物（可直接双击）
│  ├─ pet-strings.json        全部台词，UTF-8，改这个换性格
│  ├─ pet-settings.txt        饱食度衰减速度、是否置顶
│  ├─ build.cmd               一键重新编译
│  └─ assets/
│     ├─ pet-forms.json       形态清单（文件 / 标题 / 宽度）
│     ├─ whale-girl-transparent.png   Q版立绘
│     ├─ improved-1.png               长大版立绘（当前未启用）
│     ├─ whale-girl-adult.png         成年形态立绘（已抠白底）
│     ├─ web-q.png / web-tall.png     网页吉祥物用的小尺寸版
│     ├─ 图片来源与授权.md              ⚠️ 授权说明，务必先读
│     └─ LICENSE                      CC BY-NC-SA 4.0 全文
└─ deepseek-harness.html      自介网站（含网页吉祥物）
```

---

## 授权（重要，请先读）

这个项目**代码与素材的授权是分开的**。

### 代码

见 `LICENSE-CODE`（MIT）。

### 立绘素材：**不是** MIT，且**不可商用**

| 文件 | 来源 | 授权 |
|---|---|---|
| `whale-girl-transparent.png` | [fornarwhal/deepseek-whale-girl-icon](https://github.com/fornarwhal/deepseek-whale-girl-icon) | CC BY-NC-SA 4.0 |
| `improved-1.png` | 同上（去伪影修复版） | CC BY-NC-SA 4.0 |
| **`whale-girl-adult.png`** | [萌娘共享 · 女仆装鲸鱼娘.png](https://commons.moegirl.org.cn/zh-hans/File:%E5%A5%B3%E4%BB%86%E8%A3%85%E9%B2%B8%E9%B1%BC%E5%A8%98.png) | ⚠️ **原作者保留权利** |

**角色形象**：OC「溟月」，原作者 **上善无形**；DeepSeek 元素二创 **ZipZipPipe**；去伪影修复 **QYQCAMIAO**。

- `whale-girl-transparent.png` 与 `improved-1.png` 采用 **CC BY-NC-SA 4.0**：须署名、**非商用**、相同方式共享。
- `whale-girl-adult.png` 在萌娘共享的文件页被标注为「**本作品仅以介绍为目的在此百科中以非盈利性方式使用，其著作权由原著作权人保留**」，即**原作者保留权利**，并非标准 CC 授权。它随本仓库分发是出于「桌面宠物需要完整可运行」的考虑；**如需正式使用请自行联系原作者取得许可**。
- 任何情况下**不得用于商业用途**。完整说明见 `鲸鱼娘桌宠/assets/图片来源与授权.md`。

---

## 桌宠怎么跑

### 直接运行

双击 `鲸鱼娘桌宠/鲸鱼娘桌宠.exe`。

### 自己编译

需要 .NET Framework 4.x 的编译器（Windows 自带，无需安装 SDK）：

```cmd
cd 鲸鱼娘桌宠
build.cmd
```

等价的手工命令：

```cmd
"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" ^
  /nologo /target:winexe /platform:anycpu /optimize+ ^
  /out:鲸鱼娘桌宠.exe ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  Pet.cs
```

### 自检与调试开关

程序内置两个自检模式，不开窗口、直接出图和日志：

```cmd
鲸鱼娘桌宠.exe --selftest     :: 渲染各形态与各状态到 %TEMP%\whalepet\*.png，并打印物理/布局数据
鲸鱼娘桌宠.exe --dragetest    :: 真实移动光标测拖拽精度，输出每步偏差
```

`--dragetest` 会打印「抓点」与光标的偏差，用来验证拖拽是否跟手（当前实测 0.00 px）。

### 操作

| 操作 | 效果 |
|---|---|
| 左键拖动 | 抓住她（抓点精确跟随光标，不漂） |
| 松手甩动 | 按光标速度抛出，带抛物线、落地回弹与扬尘 |
| 单击 / 双击 | 戳一下 / 切换形态 |
| 右键 | 菜单：投喂 4 种食物、摸摸头、戳一下、换形态、置顶 |
| 托盘图标 | 双击显示隐藏，右键投喂 / 退出 |

### 换素材

把任意**带 alpha 通道**的 PNG 放进 `assets/`，程序会自动扫描（跳过不透明图，以及文件名含 `preview`/`icon`/`banner`/`web-` 的）。也可以用 `pet-forms.json` 精确控制顺序、标题与显示宽度：

```json
{
  "adult": false,
  "forms": [
    { "file": "whale-girl-transparent.png", "title": "Q版",     "width": 196 },
    { "file": "whale-girl-adult.png",       "title": "成年形态", "width": 200 }
  ]
}
```

每个形态会在贴图上方预留一块「气室」给顶部状态条和气泡，高度 `max(52, 66 + 0.26 × 贴图高)` —— 没有这块空间，越高的形态气泡越会盖在她自己脸上。

---

## 网页（`deepseek-harness.html`）

单文件自介页，含一个鲸鱼娘吉祥物：呼吸浮动、可点击切换台词、双形态切换（形态选择会记在 localStorage）。

吉祥物图片按**相对路径**引用 `鲸鱼娘桌宠/assets/web-*.png`，所以整个目录要一起移动；直接双击 HTML 即可打开。

---

## 一些实现上的坑（留个记录）

- **不要用 `TransparencyKey`**：它只剔除「颜色恰好等于键色」的像素，抗锯齿边缘会留一圈混色描边；而且物理每帧移动窗口时会闪。分层窗口没有这两个问题，`alpha=0` 的像素还能让点击穿透。
- **抓取要用屏幕坐标**：按下时记录「抓点相对脚底中心」的向量，之后每步用 `Cursor.Position` 把它钉回光标下；用 `MouseEventArgs` 的坐标会滞后。位置取整只做一次，否则会累积亚像素漂移。
- **文字底色必须完全不透明**：半透明底会让深色壁纸透上来，浅色文字立刻糊成一片。当前用藏青 `(26,34,66)` 配米白 `(247,249,255)`，对比度 14.76:1。
- **`MeasureString` 用 width 重载会低估中文换行高度**，会把最后一行裁掉；要改用带 `StringFormat` 的重载。
- **不要靠算法找眼睛**：这个角色头发又蓝又暗，与瞳孔/虹膜同色系，暗像素质心、蓝青虹膜、肤色定脸三种检测全都会把头发算进去。当前的眼睛位置是用「渲染帧叠加标尺线目视读数」定标的。
- **眨眼弧线别画**：在她的脸型上会被读成「长了两道眉毛」，实测很违和，已移除。

---

## License

- 代码：MIT（`LICENSE-CODE`）
- 立绘素材：CC BY-NC-SA 4.0 / 原作者保留权利，**非商用**（见上文与 `assets/`）
