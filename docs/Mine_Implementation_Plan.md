# Mine 机制实施与验证记录

最后更新：2026-07-24 03:48:44 +08:00

状态：设计 Q001-Q026 已冻结，Q012/Q013 的游戏端读取严格度已按用户后续要求修订；代码、资源、自动化测试、四 patch 离线合并/IL 校验和目标部署已完成。按用户要求，助手不得自行启动游戏，因此实机矩阵与帧耗时对照尚未执行，不能把本记录标记为完整验收。

## 目标与部署

- 目标程序集：`F:\SDEZ_165\Package\Sinmai_Data\Managed\Assembly-CSharp.dll`
- MonoMod：`F:\SDEZ_165\Package\BepInEx\core\MonoMod.dll`，程序集版本 `20.5.21.5`
- Patch：`F:\SDEZ_165\Package\BepInEx\monomod\Assembly-CSharp.MineSupport.mm.dll`
- 视觉资源：以 `MineSupport.MineVisuals` 嵌入 `Assembly-CSharp.MineSupport.mm.dll`，MonoMod 合并后由 `Assembly-CSharp` 从内存加载；源产物为 `dist\MineSupport\minevisuals`
- 实际 patch 顺序：AssetsPatch、DpPatches、MineSupport、SoflanSupport
- 部署前旧 Mine DLL/PDB 已保存为 `.predeploy-20260724_015129` 后缀；线程上下文清理修订前的 DLL 另存为 `.pre-context-reset-20260724_020500`，视觉/生命周期最终版前另存为 `.pre-final-20260724_022107`。混合修饰读取改动前的 Mine 与 Soflan DLL/PDB 均另存为 `.pre-mixed-modifiers-20260724_031602`；内置资源版前的 Mine DLL/PDB 使用 `.pre-embedded-20260724_034824`，原外置资源目录保存为 `MineSupport.pre-embedded-20260724_034824`。这些后缀都不会被 MonoMod.Loader 当成 patch 加载。

Release DLL SHA-256：

`5EBFDD3131B19B10BA9DA4DE1AC8C7AC6299A4A1D6D44D41DAB6CA6959D77F94`

SoflanSupport Release DLL SHA-256：

`0E8A4A58EF8E3082136AD77AA9B85B936B1552FDDD5E4676B28D5C51650C2022`

MineVisuals SHA-256：

`5911535D18F88DA5CDF15C178B37E670C44FDEBC5AB2C22830574AC20EF9F857`

## 已实现行为

### MA2 输入

- 在 MA2 Note 记录唯一的最终扩展字段中，以大小写敏感的 `str.Contains("!m")` 判断 Mine；大写 `!M` 不视为 Mine。
- 命中后临时移除该字段中的全部精确 `!m`，其余内容和顺序原样保留；因此 `#1!m!y`、`!y!m#1`、重复 `!m` 和未知私有修饰都可继续交给各自读取器。
- MineSupport 不再验证 `#groupFspeed` 或其他私有修饰的内部语法；错误字段位置和非 Note 记录仍拒绝，Soflan token 的合法性由 SoflanSupport 的正则解析器独立负责。
- 拒绝范围限于当前谱面/难度；Release 与 Debug 都写 Error，不让整个进程因格式错误崩溃。
- Error 至少包含谱面路径、1-based MA2 记录序号、记录类型、原始尾字段和具体原因。

### 判定与计分

- Mine 沿用基础物件原生判定窗口，不新增碰撞状态机。
- 原始 `TooFast`/`TooLate` 映射为最终 `Critical`；其他原始结果映射为最终 `TooLate`。
- 最终结果继续进入基础 Tap/Hold/Slide/Break/Touch 的分值、Combo、Life、FC/AP、Ghost 和上传结构。
- 只转换 Live、强制超时和正常收尾上下文；Ghost、目标成绩生成和无上下文 `SetResult` 直通，避免二次反转。
- Track Skip 仍由原版在转换后强制为 `TooLate`，且不批量播放反馈。
- P1/P2 使用独立 `GameScoreList`、运行时绑定、输入、音量与反馈去重状态。

### AutoPlay 与反馈

- Mine 的 `NoteCheck` 期间只让 `GameManager.IsAutoPlay()` 返回 `false`，不修改全局 `GameManager.AutoPlay`。
- 实体按键、触摸和经输入层注入的 DJAuto 仍会进入原生判定并可触雷。
- 原始 Critical/Perfect/Great/Good、Fast/Late、Break/EX、Touch、Hold Loop 和 Slide 命中反馈均被抑制。
- 只有实时输入导致的最终 `TooLate` 才播放一次原生 MISS 视觉和 `SE_GAME_TOUCH_HOLD_MISS`；未触碰成功、Ghost、Track Skip 和成绩生成静默。
- JudgeGrade/SlideJudge 继续使用各 monitor 已配置的原生显示选项；音量按最终基础计分类别读取玩家设置。

### Slide、Each 与 Touch Group

- Star 头和 Slide 本体按各自 MA2 记录独立标记、独立结果。
- `*` 同头多分支互相独立。
- ConnectSlide 的全部嵌套内部段必须与所属本体共享 Mine 状态；整条连接链仍只使用原生本体结果槽。
- 孤立 Mine ConnectSlide 或连接链混合状态会拒绝谱面。
- `calcEach` 前临时移除 Mine，调用原版重新构建剩余普通节点的 Each/Touch Group，再按原索引放回 Mine；Mine 自身的 Each/Touch 链字段清空。
- Mine 尾字段在进入原版和 Soflan hook 前只临时移除精确 `!m`，返回后完整恢复；SoflanSupport 从剩余混合字段中用正则提取 `#groupFspeed`，速度、出现时间和判定窗口保持正交。

### 视觉与资源

- Unity 2018.4.7f1、StandaloneWindows64 AssetBundle 以 `EmbeddedResource` 随 patch DLL 部署，版本 `mine-visual-v1`；运行时通过 `GetManifestResourceStream` 读取并调用 `AssetBundle.LoadFromMemory`，不依赖外置目录或临时文件。
- shader 使用 Rec.709 亮度，输出区间 0.58-1.0，并叠加静态斜线纹理和内部高对比轮廓；不使用闪烁。
- 每个运行时物件只在最派生 `Initialize` 完成后扫描一次子 `SpriteRenderer`，缓存原 `sharedMaterial`、颜色/alpha、enabled 和 EX 覆盖层 active 状态；Mine 期间同时禁止 Slide Break 动态闪烁。
- 每帧只遍历缓存数组重新施加材质，不做层级扫描或委托分配；结束、对象池复用和新一局边界幂等恢复。
- 嵌入资源缺失/损坏、版本不匹配、Material/Shader 缺失或 shader 不支持时，只拒绝含 Mine 的谱面；普通谱面不触发资源门禁。
- 资源 Error 包含程序集资源名、patch 版本、期望资源版本、失败阶段和异常类型，使用一次性 key 防止逐物件刷屏。

### 生命周期与日志

- `GamePlayManager.Initialize` 在新局和 Quick Retry 共用入口先清空运行时绑定、分数索引、视觉快照和反馈去重状态，再由各 `GameScoreList.Initialize` 重建 Mine 索引。
- 谱面格式、资源或 Slide 链加载失败也执行相同幂等清理。
- `dpMineSupport.log` 使用后台队列、UTF-8 without BOM；每行包含 UTC ISO-8601 时间、线程 ID 和 INFO/ERROR 级别。
- `WriteLine` 仍仅在 Debug 编译存在；`Error`/`ErrorOnce` 在 Release 中无条件写文件并尝试转发 `UnityEngine.Debug.LogError`。

## 主要代码边界

- `MineTailParser.cs`：大小写敏感的 `Contains("!m")` 判定与非 Mine 修饰保留。
- `MineChartLoader.cs`：谱面预检、错误元数据与连接 Slide 一致性。
- `Manager.NotesReader.Mine.mm.cs`：尾字段净化、Soflan 兼容、Each/Touch 重建与资源门禁。
- `MinePolicyCore.cs`、`MineRuntime.cs`：结果来源、每玩家绑定、AutoPlay、反馈调度与生命周期。
- `Manager.GameScoreList.Mine.mm.cs`：统一正式结果转换边界。
- `Manager.GameManager.Mine.mm.cs`：上下文感知的 AutoPlay 查询。
- `Manager.GamePlayManager.Mine.mm.cs`：新局/重试瞬态清理。
- `MineVisual.cs`：嵌入 AssetBundle 读取、内存加载、版本验证、视觉缓存与对象池恢复。
- `Monitor.*.Mine.mm.cs`：各物件族判定、反馈和视觉接入。
- `PatchLog.mm.cs`：Release 可用的异步 Error 日志。

## 离线验证证据

以下验证均在 2026-07-24 重新执行：

- CoreTests：PASS。覆盖 Contains 判定、混合 `!m`/`!y`/Soflan 排列、重复 Mine、大小写、Live/Forced/Natural 与 Ghost/Generated 来源策略和结果二值反转。
- ChartTests：PASS。覆盖字段位置、错误原因、三段嵌套连接 Slide 的全链一致性，并验证生成的 661 Note 全 Mine 谱。
- SoflanMarkerTests：PASS。覆盖 `#1!m!y`、`!y!m#1`、FixedSoflan、无 marker、重复 marker、非法 group 和非法 fixed speed。
- SoflanLogTests（Release）：PASS。实际写出带 `[ERROR]` 的 UTF-8 无 BOM `dpSoflanSupport.log`。
- LogTests（Release）：PASS。实际写出带 UTC/线程/ERROR 的 UTF-8 无 BOM 日志。
- AssetBundle 构建：PASS。Unity 2018.4.7f1 构建后重新打开资源，校验版本 TextAsset、Material 和 Shader。
- 嵌入资源：PASS。Patch 与四 patch 合并输出均包含 `MineSupport.MineVisuals`；长度 `14060`、UnityFS 头和 SHA-256 与源 AssetBundle 完全一致。
- Debug 构建：PASS，0 warning，0 error。
- Release 构建：PASS，0 warning，0 error。
- 四 patch 离线合并：PASS。使用目标 BepInEx MonoMod，按真实顺序合并 Assets/Dp/Mine/Soflan。
- IL 校验：PASS。确认 Mine `String.Contains/Replace`、Soflan `Regex.Matches`、Mine 净化期间的 NotesReader/Soflan hook、`GetManifestResourceStream`/`AssetBundle.LoadFromMemory` 且无 `LoadFromFile`、上下文 AutoPlay、SetResult/FinishPlay、生命周期清理、资源缓存、EX 覆盖恢复、Slide Break 闪烁抑制与热点 NoteCheck 无 Action/Func 分配。
- 合并输出：`D:\temp\MineSupport.Validation\Assembly-CSharp.dll`，SHA-256 `E9E3B1FE812555928E52728DC1293D7EA3B6A02AD7B6E5EEF050BC8492A6B898`。
- 合并输出仍保留一个 Cecil 未清理的、未使用的 `MonoMod` AssemblyRef；没有 Mine 类型或方法引用 MonoMod。目标 BepInEx core 本身也提供对应程序集，验证器对此只记录说明，不虚报为“完全无引用”。

全量测试谱由 `tools/MineSupport.ChartGenerator` 使用目标 `MA2Record` 生成：

- 文件：`tests/charts/mine_full_111960_02.ma2`
- Note 数：661
- 类型数：19，包含 `BRHLD`、Break/EX、Touch/TouchHold、Star、Wifi 与 `CNS*` ConnectSlide。

## 未执行的实机验收

用户明确要求助手不得自行启动游戏。`mai2.ini` 已恢复为测试前文件，当前值与备份的 SHA-256 均为 `D58119AFFAC1527F39F321EE38CE14E0268BBA632646FABD1DB8E7F138718F58`，且没有 `Sinmai`、`amdaemon` 或 `inject` 进程。因此以下项目保留为用户执行项：

- 普通谱面零回归与全物件 Mine 灰阶/纹理显示。
- AutoPlay 避雷、实体输入/DJAuto 触雷及单次 MISS 视觉/声音。
- Slide 头/本体、同头分支和连接链实机表现。
- Track Skip、Ghost、Quick Retry、正常收尾。
- 1P/2P 独立结果和用户显示/音量设置。
- 多皮肤与对象池反复复用后的材质、颜色、alpha、enabled 恢复。
- 无效 Mine 与嵌入资源损坏/版本不匹配的游戏内加载失败 UI/日志。
- 密集谱暖机后的 GC 分配、层级扫描和帧耗时普通基线对照。

在上述矩阵由用户完成并保留日志/截图/性能数据前，项目状态应写作“已部署，离线验证通过，实机待验”，不能写作“完整完成”。
