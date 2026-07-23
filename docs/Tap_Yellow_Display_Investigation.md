# Tap 黄色显示机制调查

状态：仅完成源码调查与方案边界确认，本文档不代表已修改游戏或 MineSupport 补丁。

## 结论

游戏当前没有一个现成的、独立的“黄色显示”开关。

最接近的状态是 `NoteData.isEach`，但它表达的是 Each 语义，不是单纯的颜色。对于 Tap，`isEach` 最终会同时切换主体 Sprite、Guide 样式和 EX 覆盖层颜色；在数据层，它还与 `indexEach`、`eachChild` 和 Each 分组有关。因此，不应为了让单个非 Each Tap 变黄而伪造 `isEach = true`。

不过，Tap 黄色主体本身使用的是已经存在的 `EachTap` Sprite。若只切换 Tap 主体所用的 Sprite，并保持真实的 `isEach` 不变，就可以做到：

- 判定类型和判定窗口不变；
- 出现、移动和消失时机不变；
- 得分和 Combo 逻辑不变；
- 不生成 Each 分组；
- 不显示 EachGuide；
- 不需要新增黄色图片资源。

因此，底层操作很简单，但需要新增一个独立的显示条件，不能复用原有的 Each 开关。

## 当前 Each 的生成方式

### MA2 没有黄色或 Each 字段

原版 `TAP` 记录只包含记录类型、bar、grid 和位置，没有黄色或 Each 参数：

- `D:\sdez_165\Manager\Ma2fileRecordID.cs:217`

`NoteData.isEach` 在新建和清理时默认为 `false`：

- `D:\sdez_165\Manager\NoteData.cs:49`
- `D:\sdez_165\Manager\NoteData.cs:99`

### Each 是加载后自动推导的

MA2 加载完成后，`NotesReader.calcEach()` 遍历同一 grid 的物件，并根据物件类型组合设置 `isEach = true`：

- `D:\sdez_165\Manager\NotesReader.cs:780`
- `D:\sdez_165\Manager\NotesReader.cs:1549`

主要规则如下：

1. `ConnectSlide` 不参与 Each。
2. Slide 只与同一时间的 Slide 组成 Each。
3. 非 Slide 物件可与同一时间的其他非 Slide 物件触发 Each。
4. 时间相等比较的是谱面 grid，而不是渲染帧或浮点毫秒。

因此，游戏不会保留 simai 中 `/` 本身的信息。simai 转成 MA2 后，游戏看到的是同一 grid 上的多条物件记录，再自行推导 Each。

## Tap 的黄色显示链路

普通 Tap 和 EX Tap 都由 `TapNote` 处理：

- `D:\sdez_165\Monitor\Game\GameCtrl.cs:1351`

初始化时，`NoteBase.Initialize()` 最终调用：

```csharp
SetEach(note.isEach);
```

位置：

- `D:\sdez_165\Monitor\NoteBase.cs:253`

`TapNote.SetEach()` 根据该参数切换显示资源：

- `D:\sdez_165\Monitor\TapNote.cs:10`

| 显示部分 | 非 Each | Each |
| --- | --- | --- |
| Tap 主体 | `GameNoteImageContainer.NormalTap[...]` | `GameNoteImageContainer.EachTap[...]` |
| Guide | `NoteGuide.Color.Normal` | `NoteGuide.Color.Each` |
| EX 覆盖层 tint | `TapColor` | `EachColor` |
| 内部缓存 | `EachFlag = false` | `EachFlag = true` |

这说明黄色外观不是一次统一的 `SpriteRenderer.color = yellow`。其中：

- Tap 主体主要依靠专用黄色 Sprite；
- Guide 使用另一张 Each Guide Sprite；
- `EachColor` 只用于 EX 覆盖层，不负责把整个 Tap 主体染黄。

## 黄色资源

游戏启动时会为五种 Tap Design 分别加载普通、Each 和 EX Sprite：

```csharp
_normalTap[i] = Resources.Load<Sprite>("Process/Game/Sprites/Tap/Tap_" + text);
_eachlTap[i] = Resources.Load<Sprite>("Process/Game/Sprites/Tap/Tap_Each_" + text);
_exTap[i] = Resources.Load<Sprite>("Process/Game/Sprites/Tap/Tap_Ex_" + text);
```

位置：

- `D:\sdez_165\Process\GameNoteImageContainer.cs:343`

资源范围为 `Tap_Each_00` 至 `Tap_Each_04`，覆盖 Default、Legacy、Bear、Bar 和 Any 五种 Tap Design。因此，将普通 Tap 主体显示为黄色不需要制作新资源。

## 现有字段能否作为简单开关

| 字段或资源 | 能否作为纯黄色开关 | 原因 |
| --- | --- | --- |
| `NoteData.isEach` | 不能 | 它是分组语义，并会经 `SetEach()` 同时改变 Guide 和其他 Each 状态。 |
| `NoteBase.EachFlag` | 不能 | 它只是受保护的内部缓存；单独赋值不会自动切换 Sprite。 |
| `NotesEffectColorTable.EachColor` | 不能 | 它只 tint EX 覆盖层，不会替换 Tap 主体。 |
| `GameNoteImageContainer.EachTap` | 可以作为显示资源 | 直接选择该 Sprite 能让主体变黄，但当前没有独立配置或公开方法负责该选择。 |

所以，原版不存在可直接调用的 `ShowYellow`、`IsYellow` 或配置项。最小的独立控制点是 `SpriteRender.sprite` 的资源选择。

## 对判定和时机的影响

对于 Tap，`TapNote.SetEach()` 本身不修改以下字段：

- `AppearMsec`、`StartMsec`、`TailMsec`；
- `JudgeType` 和判定窗口；
- `NoteIndex`、按钮位置；
- 得分类型和正式结算结果。

Tap 的 `EachFlag` 在设置后也没有参与 Tap 判定路径。因此，只替换主体 Sprite 是纯显示操作。

需要注意的是，这不等于可以安全地修改 `note.isEach`。`note.isEach` 在显示前还会参与 Each 数据关系和 Guide 初始化：

- `GuideObj.Initialize(GetEachAngle(note), note.indexEach)`：`D:\sdez_165\Monitor\NoteBase.cs:237`
- `eachChild/indexEach` 构造：`D:\sdez_165\Manager\NotesReader.cs:797`

若伪造 `isEach`，即使 Tap 判定本身不变，也可能产生黄色 Guide、错误的 Each 范围或与其他物件状态不一致。

## 推荐的状态分离

若需要让某类非 Each Tap（例如 Mine Tap）显示黄色，应保留两套独立条件：

- `isEach`：只表达真实的同时押关系，并继续控制 EachGuide、Each 分组和原版 Each 行为。
- 独立显示条件，例如 `isMine` 或 `displayYellow`：只控制 Tap 主体是否选择 `EachTap` Sprite。

概念上的选择关系应为：

```text
主体 Sprite = (真实 Each 或独立黄色条件) ? EachTap : NormalTap
Guide 样式 = 真实 Each ? Each : Normal
EachFlag    = 真实 Each
```

如果希望 EX Tap 的覆盖层也呈现与 Each 相同的黄色，可额外根据独立黄色条件选择 `EachColor`；这仍不需要改变 Guide 或 `EachFlag`。

## 与 MineSupport 当前实现的关系

当前 MineSupport 已有独立的 `NoteData.isMine` 和运行时 Mine 状态，因此不需要借用 `isEach` 表示 Mine：

- `Manager.NoteData.Mine.mm.cs`
- `Manager.NotesReader.Mine.mm.cs`
- `MineRuntime.cs`

当前 `MineVisual.Apply()` 会遍历物件下的全部 `SpriteRenderer` 并应用灰色 tint：

- `MineVisual.cs:14`

这适合现有的统一灰色实验方案，但它不是“只让 Tap 主体使用黄色 Each Sprite”的精确控制方式，因为遍历全部子 Renderer 可能同时覆盖 Guide、EX 层和其他子物件。

若后续把 Mine Tap 改为黄色，适合在 `Monitor.TapNote.Mine.mm.cs` 的 `orig_Initialize(note)` 完成后，根据独立 Mine 状态覆盖 Tap 主体 Sprite，同时保留原版已经依据真实 `note.isEach` 设置好的 Guide 和判定状态。其他物件类型是否采用黄色，应分别调查其专用 Sprite 和运行时刷新逻辑，不能直接把 Tap 结论推广到 Hold、Star、Touch 或 Slide。

## 对象池注意事项

Tap 使用对象池复用。任何新增的显示状态都必须在每次 `Initialize()` 时明确恢复或重新选择 Sprite，不能只在黄色条件为 `true` 时覆盖一次，否则可能让上一颗黄色 Tap 的 Sprite 残留到下一颗普通 Tap。

原版 `Initialize()` 每次都会经 `SetEach(note.isEach)` 重新选择 Normal/Each Sprite。补丁若在原版初始化之后覆盖黄色 Sprite，应确保非黄色路径仍由原版初始化完成恢复，或显式恢复为基于真实 `isEach` 的正确 Sprite。

## 最终判断

1. 原版没有独立的纯黄色开关。
2. `isEach` 不能作为该开关，因为它同时承载 Each 语义和 Guide 行为。
3. Tap 主体变黄的实际底层操作只是选择现成的 `EachTap` Sprite。
4. 使用独立状态控制 Sprite，可以不影响判定、出现时机、得分和 EachGuide。
5. 在 MineSupport 中应直接使用已有的 Mine 状态作为显示条件，不需要新增或伪造 Each 关系。
