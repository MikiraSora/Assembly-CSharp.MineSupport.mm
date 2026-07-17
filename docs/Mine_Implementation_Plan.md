# Mine 功能移植实施记录

状态：已完成第一版 MonoMod patch，Debug/Release 编译通过，已完成离线 MonoMod 合并验证；尚未在实际游戏流程中回归。

## 参考资料

- `F:\MajdataEdit\docs\Mine_Implementation_Audit.md`
- `F:\MajdataView\docs\Mine功能与实现细节.md`

参考资料只用于确认 Mine 的语义和 MA2 标记契约。本项目没有复制 MajdataView 的 Unity 物件实现，实际运行逻辑全部针对游戏的 `Assembly-CSharp.dll` 重新组织。

## 目标环境

- 目标程序集：`F:\SDEZ_165\Package\Sinmai_Data\Managed\Assembly-CSharp.dll`
- 部署方式：BepInEx `MonoMod.Loader` 加载 `Assembly-CSharp.MineSupport.mm.dll`
- 编译目标：`.NET Framework 4.7.2`，引用游戏随附的经典 `MonoMod.dll`
- 运行时依赖：Mine patch 不新增外部运行时 DLL；`MonoMod` 引用只用于 patch 元数据，离线合并后不会保留在目标程序集引用中

## 已确认的产品决策

1. **输入格式**：使用 MA2 标准记录尾字段的精确小写 `!m`。支持单独的 `!m`，以及与颜色/其他修饰字段同处一个尾字段的 `!m#...`、`#...!m` 形式；不新增 MA2 记录类型。
2. **结果反转**：Mine 原始结果为 `TooFast` 或 `TooLate` 时，正式结果记为 `Critical`；其他任意 `ETiming` 记为 `TooLate`。物件内部的原始结果仍保留到正式结算边界。
3. **覆盖范围**：所有带 `!m` 的标准记录启用 Mine，优先于 Break、EX、Each 的表现和分类，不重复生成结果。
4. **视觉方案**：第一版使用 `SpriteRenderer.color` 灰色 tint，统一经过可替换的 `MineVisual` 后端处理，保留 alpha；后续可替换为 shader/material，不改变输入、状态和结算层。
5. **AutoPlay**：Mine 物件跳过 AutoPlay 自动按下、自动保持、自动触点和自动滑动，等待普通判定窗口自然超时并按 Mine 规则结算；普通物件保持原版 AutoPlay，手动输入和 DJAuto 仍可触发 Mine。
6. **统计方案**：采用方案 B，不增加独立 Mine 统计桶或独立分母。Mine 沿用 Tap/Break/Hold/Slide/Touch 的底层 `SetResult`、权重、Combo 和已有上传结构。
7. **可见反馈**：Mine 保留内部判定、`SetResult`、Combo 更新、超时和销毁，但抑制玩家可见的 JudgeGrade、Fast/Late、普通判定音、Just/烟花、Hold/Break 闪光、Touch 特效和 Slide 成功对象。
8. **Slide 粒度**：按 MA2 记录粒度处理。星头和轨迹可以分别带 `!m`；ConnectSlide 不额外传播标记，也不额外制造结果。
9. **回归范围**：覆盖无标记回归、所有物件族、Mine 与 Break/EX/Each 组合、AutoPlay 避让、Slide 粒度、对象池颜色恢复和普通物件反馈。

## 数据流

1. `Manager.NotesReader.loadNote` 调用原版解析后，读取当前 `MA2Record` 的尾字段并写入扩展字段 `NoteData.isMine`。
2. `Manager.NotesReader.loadMa2Main` 完成原版 `calcEach` 后，移除 Mine 与普通 Each 的关联，并清理 Mine 的 `parent/indexEach/eachChild`，避免 Mine 改写其他普通物件的 Each 关系。
3. 各类物件在 `Initialize` 后绑定运行时状态和 `(GameScoreList, noteIndex)`，对象池复用时重新写入状态并恢复旧颜色。
4. `Manager.GameScoreList.SetResult` 作为统一正式结算边界，查询 Mine 索引并执行结果映射，再调用原版统计逻辑。

## Patch 分层

### 输入与状态

- `Manager.NoteData.Mine.mm.cs`：增加 `isMine`，并在 `clear()` 中显式重置。
- `Manager.NotesReader.Mine.mm.cs`：识别 `!m`、传播标记、清理 Each 关系。
- `MineRuntime.cs`：维护运行时 Mine 状态、分数索引、AutoPlay 临时避让、反馈抑制深度和结果映射。

### 视觉

- `MineVisual.cs`：保存每个 `SpriteRenderer` 的原始颜色，应用灰色 RGB tint，保留 alpha，并在对象池复用/结束时恢复。
- Tap/Star、NoteBase、Hold/BreakHold、Break、Touch、SlideRoot/SlideFan、BreakStar 的 `Initialize/Execute` 均经过该后端；Slide 动态生成的箭头也通过子 SpriteRenderer 覆盖。

### 判定与反馈

- `Manager.GameScoreList.Mine.mm.cs`：统一执行正式结果反转。
- `Monitor.NoteBase.Mine.mm.cs` 及 Hold/BreakHold/Break/Touch/Slide 派生 patch：包装 `NoteCheck`，Mine 时临时将 `GameManager.AutoPlay` 设为 `None`，保持普通判定窗口和自然超时。
- `JudgeGrade`、`TouchEffect`、`SlideJudge`：在反馈抑制期间直接跳过 Mine 的显示和特效。
- 专用入口额外覆盖：Break/Hold/BreakHold 闪烁、BreakNote 判定音、TouchHold 循环音、Slide 路径命中音、BreakStar 滑动 Break 特效。

### 日志

- `PatchLog.mm.cs` 会被 MonoMod 作为 `MineSupport.PatchLog` 注入目标程序集。
- 日志文件名为 `dpMineSupport.log`，采用后台队列写盘；当前 `WriteLine` 为 Debug 条件调用，Release 不产生 Mine 调试日志。
- Debug 下会记录 Mine 运行时绑定和正式结果映射，便于定位谱面标记传播或结算索引问题。

## 验证记录

### 编译

- `dotnet build .\Assembly-CSharp.MineSupport.mm.csproj --configuration Debug`：通过，0 警告，0 错误。
- `dotnet build .\Assembly-CSharp.MineSupport.mm.csproj --configuration Release`：通过，0 警告，0 错误。

### 离线 MonoMod 合并

使用 BepInEx 同版本 `MonoMod.dll`、`Mono.Cecil.dll` 和隔离的目标程序集副本运行 `D:\sdez165_soflan_support_tools\Patcher\bin\Release\Patcher.exe`，结果通过：

- 输出包含 `MonoMod.WasHere`。
- 输出包含 `MineSupport.MineRuntime`、`MineSupport.MineVisual`、`MineSupport.PatchLog`。
- `Manager.NoteData` 包含 `isMine`，且 `clear()` 会重置该字段。
- `NotesReader.loadNote/loadMa2Main` 包含 Mine 标记传播和 Each 清理调用。
- `GameScoreList.SetResult` 包含 Mine 查找和结果映射调用。
- 输出没有残留 `patch_*` 类型，也没有 `MonoMod` 运行时程序集引用。
- Break/Hold/BreakHold/TouchHold/Slide 的专用反馈抑制入口均已在合并输出中确认存在。

## 尚未完成的验证

- 需要在实际 BepInEx 游戏环境加载 `.mm.dll`，用包含普通 Tap、Break/EX、Hold、Touch/TouchHold、Star、Slide/FanSlide/ConnectSlide 和 `!m` 组合的 MA2 谱面回归。
- 需要确认实际对象池、资源层级和游戏版本对 `GetComponentsInChildren<SpriteRenderer>(true)` 的覆盖是否完整。
- 需要确认实机中 AutoPlay 超时、DJAuto 手动触发、Combo/成绩上传和 Mine 灰色 tint 的最终表现。

上述项目是实机验证项，不代表当前 patch 编译或离线合并失败。
