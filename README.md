# StarPawn

《Probably Stolen》的统一经营扩展：实物鉴定、金银珠宝、翡翠加工、收藏品、娱乐商品、银行借贷、证券行情及典当。

当前可运行版本为 **0.3.0 / Windows / 100-Rev13 / Steam BuildID 26525552**。玩法改版与独立存档实机验收已完成本轮交付；新增图片为几何验证用开发素材，最终美术待用户确认小样。

本轮增加金银五类各两款、六类翡翠原石及四类成品、五件实体仪器、七册固定缺陷图鉴、八格实体手册收纳；补齐银行日限额、新合同利率、资金总览与夜间结算回执。精确范围与限制见 [0.3交付记录](docs/REVISION_03.txt)。239个目录定义包含状态组合，不等于239种独立造型。

## Claude 美术工作入口

先阅读 [美术交接](art/ART_DIRECTION.txt)、[最小小样 prompt](art/CLAUDE_STYLE_PROMPT.txt) 和 [下一版需求](docs/REVISION_03.txt)。

第一轮只提交一个黄金戒指的三状态风格小样，等用户确认后再制作整套物品及 UI。设计范围见 [asset-plan.csv](art/asset-plan.csv)，精确运行文件与状态映射见 [runtime-art-map.json](art/runtime-art-map.json)，界面设计规格见 [UI_SPEC.txt](art/UI_SPEC.txt)。existing-assets.json与existing-items.json为0.2历史快照。

先拉取 `gameplay/0.3`，再创建美术分支 `art/style-sample`。样稿放 `art/proposals/`；经确认的素材再进入 `mod/Textures/`。保持物品 ID、证据 ID、接口和存档兼容；改热点坐标时同步配置并检查。

## 目录

- `source/Core`：无 Unity 依赖的资产、鉴定、经济规则。
- `source/Game`：原生物品、PaperUI、钱包、Harmony 和存档适配。
- `mod/Content`：物品、规则、本地化与可选扩展包。
- `mod/Textures`：游戏加载的物品与界面贴图。
- `tests`：业务测试、原生边界替身、元数据检查；`*-results-03.txt`为0.3结果，其余保留历史基线。
- `art`：美术设计任务、状态契约和资产清单。
- `docs`：玩法改版、扩展格式和参考调研。

## 本机构建

需要 .NET SDK 8.0.424、netstandard2.1 引用包与合法安装的游戏。仓库不附带游戏程序集。

```powershell
./source/Build.ps1 -GameDir 'D:\Program Files (x86)\steam\steamapps\common\Probably Stolen'
```

构建生成 `mod/StarPawn.Core.dll` 和 `mod/StarPawn.Game.dll`。退出游戏后，把 `mod` 内容放到游戏的 `Mods/starpawn`，启用后重启。进入店铺按 **F7**，或用原版放大镜检查本模组实物。仪器可以右键选择“使用”再点目标，也可在验货台进入“检测仪器”。

清单中的 `0.46` 是该版本加载器使用的兼容字段；实际验收基线是100-Rev13。

## 验证

```powershell
./tests/Run-EconomyTests.ps1
./tests/Run-ContentTests.ps1
./tests/Run-ContentBootstrapTests.ps1 -GameDir '<game directory>'
./tests/Run-CommerceTests.ps1 -GameDir '<game directory>'
./tests/Run-NativeTradeTests.ps1 -GameDir '<game directory>'
./tests/Run-BankCardTests.ps1 -GameDir '<game directory>'
./tests/Run-AppraisalTests.ps1 -GameDir '<game directory>'
./tests/Test-HarmonyBindings.ps1 -GameDir '<game directory>'
python ./tests/Check-DevelopmentGeometry.py
```

0.3：156项业务/边界测试、33项Harmony参数元数据检查通过。196个新商品状态、284个证据中心通过几何检查。另以slot5做旧档升级、仪器、手册/收纳、原石加工、额度、正常睡眠与重载实测；替身和元数据测试不等同所有Unity异常路径实测。存档与图文报告保留本地。

内容生成需要Python与Pillow。`Generate-Catalog.py`默认只生成数据/本地化；`--development-art`会覆盖开发素材。Claude提交获批图片后不要运行覆盖式生成器。可用 `python source/Content/Export-ArtMap.py`单独更新文件映射。

## 合作与素材

仓库包含本项目源码、配置与本项目生成的旧版素材。游戏资产、第三方Mod二进制、个人存档、日志和备份保留在本地。参考玩法采用独立实现；引用或改编第三方代码/素材前核对相应许可。当前尚未指定项目开源许可证。
