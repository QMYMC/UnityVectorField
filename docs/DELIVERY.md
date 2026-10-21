# 本地交付清单

## 修改

| 文件 | 内容 |
| --- | --- |
| `.gitignore` | 排除本地保留文件、验证副本、日志和临时产物 |
| `Assets/Scripts/WindFieldData.cs` | 通用网格及纹理所有权；加载/卸载事件、直接采样 |
| `Assets/Scripts/WindParticleSystem.cs` | Short 有效区出生、自动/固定数量、单位映射、实例资源及任务取消 |
| `Assets/Scripts/StreamlineSystem.cs` | 保留 Long RK4；局部坐标、独立材质、取消旧任务和物理配色 |
| `Assets/Scripts/WindColorRamp.cs` | 实例色带、显式更新、延迟纹理创建 |
| `Assets/Shaders/WindFieldCompute.compute` | XYZ 非等距采样、有效性、按面积/体积出生、平面约束 |
| `Assets/Shaders/WindParticleRender.shader` | 保留短粒子轮廓；Transform、色标单位及 Alpha |
| `Assets/Shaders/StreamlineRender.shader` | Long 的 Transform、局部宽度与色带 Alpha |
| `Assets/Materials/Mat_FlowParticle.mat` | 仅清理已丢失、且当前着色器不使用的 `_MainTex` 引用；保留其他用户参数 |
| `Assets/Editor/VectorFieldGpuSmokeCheck.cs` | 可重现生成示例、实际 Play/GPU 验收和真实截图 |
| `Assets/Tests/EditMode/VectorField.EditModeTests.asmdef` | 原有坐标测试引用移出的适配程序集 |
| `ProjectSettings/EditorBuildSettings.asset` | 默认构建场景指向公开合成演示 |

## 新增

- `Assets/Scripts/Core/VectorFieldGrid.cs`：不可变 XYZ 网格、映射与捕获帧。
- `Assets/Scripts/Core/VectorFieldSpawnTable.cs`：按面积/体积分配的出生表与独立数量参数。
- `Assets/Scripts/VectorFieldInstance.cs`、`VectorField.Runtime.asmdef`：直接嵌入门面与运行时程序集。
- `Assets/Adapters/VectorFieldText.cs`、`VectorFieldFileLoader.cs`、`VectorField.Adapters.asmdef`：显式文件格式、可取消解析与可选地理适配。
- `Assets/Samples/Scripts/SyntheticVectorField.cs`、`VectorFieldDemo.cs`、`Assets/Samples/VectorField.Samples.asmdef`：公开合成场与可独立省略的演示 UI。
- `Assets/Samples/Scenes/VectorFieldDemo.unity`：默认演示场景。
- `Assets/Samples/Data/shallow.xyz.txt`、`deep.xyz.txt`：可重生成的数据。
- `Assets/Tests/EditMode/VectorFieldGridTests.cs`、`VectorFieldTextTests.cs`：新增算法与格式测试。
- `Tools/prepare_public.py`、`Tools/check_assets.py`：候选清单、历史扫描、文件哈希及资源/图片检查。
- `README.md`、`docs/VALIDATION.md`、`docs/PUBLICATION.md`、本清单、`docs/images/long-mode.png`、`short-mode.png`、`PUBLISH_FILES.txt`。
- 新增 Unity 目录及资源均配有 `.meta`；逐文件清单包含这些元数据。

## 移动与保留

- `WindCoordinateMath.cs`、`WindFieldSnapshot.cs` 及其原 GUID 从 Core 移至 `Assets/Adapters`，保留原有辅助能力及测试。
- `FreeFlyCamera.cs` 及原 GUID 移至 `Assets/Samples/Legacy`；用户原有 `WindDemoUI.cs` 同处该目录，文件输入改为调用独立适配器。
- 原 `SampleScene.unity` 连同 `.meta` 保存在 `LocalOnly/LegacyScene`，用户修改未覆盖。原始数据及 `.meta` 保存在 `LocalOnly/OriginalData`。旧自述设计/计划记录保存在 `LocalOnly/LegacyDocs`，均不进入候选。
- 开始工作时已有的材质、场景、包和版本修改另留备份于 `LocalOnly/BeforeUpgrade`。`StreamlineSkeleton.mat`、`WindParticleBackground.mat`、manifest、lock、ProjectVersion 的内容与该备份逐字节一致；Short 材质除上述失效引用外保留用户改动。
- 没有修改参考项目；没有导入它的程序集、业务输入、色带文件或场景资源。公开快照以独立初始提交发布，原型历史保持原样。

## 结果与下一步

42 项 EditMode 测试和 83 项 Play/GPU 断言通过，详见 [验证记录](VALIDATION.md)。公开候选资源引用及图片元数据检查通过；源码/配置模式扫描未发现目标密钥、个人绝对路径或内部地址。原始审计结果仅保存在 `LocalOnly/Audit`，不作完整安全审计承诺。

所有者已确认候选文件可公开，并选择 MIT。**原型历史仍未清理，因此不随快照上传。**候选清单见根目录 `PUBLISH_FILES.txt`；本地 `CleanValidation/Review` 为不含旧 Git 历史的候选副本。
