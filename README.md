# StarPawn

《Probably Stolen》的统一经营扩展：实物鉴定、金银珠宝、翡翠加工、收藏品、娱乐商品、银行借贷、证券行情及典当。

当前可运行基线为 **0.2.0 / Windows / 100-Rev13 / Steam BuildID 26525552**。新一轮玩法与美术改版正在开发；当前图片为待替换的旧版素材，不代表已获认可的目标画风。

## Claude 美术工作入口

先阅读 [美术交接](art/ART_DIRECTION.txt)、[最小小样 prompt](art/CLAUDE_STYLE_PROMPT.txt) 和 [下一版需求](docs/REVISION_03.txt)。

第一轮只提交一个黄金戒指的三状态风格小样，等用户确认后再制作整套物品及 UI。完整资产清单见 [asset-plan.csv](art/asset-plan.csv)，现有图像与定义的映射见 [existing-assets.json](art/existing-assets.json) 和 [existing-items.json](art/existing-items.json)。

建议美术分支 `art/style-sample`。样稿放 `art/proposals/`；经确认的素材再进入 `mod/Textures/`。玩法开发分支为 `gameplay/0.3`。美术阶段保持物品 ID、证据 ID、接口和存档兼容；改热点坐标时同步配置并检查。

## 目录

- `source/Core`：无 Unity 依赖的资产、鉴定、经济规则。
- `source/Game`：原生物品、PaperUI、钱包、Harmony 和存档适配。
- `mod/Content`：物品、规则、本地化与可选扩展包。
- `mod/Textures`：游戏加载的物品与界面贴图。
- `tests`：业务测试、原生边界替身、元数据检查及0.2.0基线结果。
- `art`：美术设计任务、状态契约和资产清单。
- `docs`：玩法改版、扩展格式和参考调研。

## 本机构建

需要 .NET SDK 8.0.424、netstandard2.1 引用包与合法安装的游戏。仓库不附带游戏程序集。

```powershell
./source/Build.ps1 -GameDir 'D:\Program Files (x86)\steam\steamapps\common\Probably Stolen'
```

构建生成 `mod/StarPawn.Core.dll` 和 `mod/StarPawn.Game.dll`。退出游戏后，把 `mod` 内容放到游戏的 `Mods/starpawn`，启用后重启。进入店铺按 **F7**，或用原版放大镜检查本模组实物。

清单中的 `0.46` 是该版本加载器使用的兼容字段；实际验收基线是100-Rev13。

## 验证

```powershell
./tests/Run-EconomyTests.ps1
./tests/Run-ContentTests.ps1
./tests/Run-ContentBootstrapTests.ps1 -GameDir '<game directory>'
./tests/Run-CommerceTests.ps1 -GameDir '<game directory>'
./tests/Run-NativeTradeTests.ps1 -GameDir '<game directory>'
./tests/Run-BankCardTests.ps1 -GameDir '<game directory>'
./tests/Test-HarmonyBindings.ps1 -GameDir '<game directory>'
```

0.2.0基线：124项业务/边界测试、33项Harmony参数检查通过。另做独立存档实机购销、银行/卡片、典当、加工、睡眠和保存重载；替身测试不等同所有Unity异常路径实测。新版本的测试结果单独记录。

## 合作与素材

仓库包含本项目源码、配置与本项目生成的旧版素材。游戏资产、第三方Mod二进制、个人存档、日志和备份保留在本地。参考玩法采用独立实现；引用或改编第三方代码/素材前核对相应许可。当前尚未指定项目开源许可证。
