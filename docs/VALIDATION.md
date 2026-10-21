# 验证记录

日期：2026-10-21。区分下列检查层级，不将编译结果代替实际显示验收。

## 环境

- Unity 2022.3.62f3c1（1623fc0bbb97）、Built-in 管线。
- Windows，NVIDIA GeForce RTX 3060 Ti，Direct3D 11。
- 最终代码通过候选文件导出到没有 `.git`、Library、原始数据和原型旧场景的本地目录；Unity 日志确认因不存在 AssetDatabase 而重建 Library。Unity 通过原有 manifest/lock 和正常 UPM 环境恢复包；没有复制原项目 Library 或指定本机数据路径。
- 原项目已有编辑器会话，验证使用另外的批处理副本；未关闭用户会话。一个测试批处理完成并保存结果后卡在退出阶段，结束该验证进程后继续运行，不将其退出行为计为画面通过依据。

## 已通过

| 层级 | 实际结果 |
| --- | --- |
| Unity 导入与编译 | 干净候选导入、C# 编译、场景及材质依赖加载成功；没有迁入业务程序集或 DLL |
| EditMode 算法测试 | **42/42 通过，0 失败、0 跳过**。包括原有 RK4、坐标/纹理数学及新增 XYZ、缺测、数组所有权、单位、计数、透明出生区、取消、平面与文本格式检查 |
| Play / GPU | **83 项断言通过**；不把断言数量作为性能指标 |
| 实际图像 | Long / Short 各一张 1440×900 真实 Camera.Render 输出，使用同一浅层合成场和相同相机；已人工查看。另检查深层和变换后的两模式运行图像 |

Play/GPU 检查覆盖：

1. 浅层 `40×12×40`、深层 `28×24×18` 两份合成场，Long 和 Short 都生成非空渲染。
2. Long → Short → Long 切换，旧模式资源释放；实际重新加载场景后两模式恢复显示。
3. 根对象平移、旋转、非均匀缩放后的两模式渲染；世界速度映射与 Transform 一致。
4. 两实例复用源材质和计算着色器时，运行时资源独立；色带、速度和数量互不覆盖，固定计数单位正确。
5. 暂停显示速度后，16 个 GPU 样本与 CPU 原始物理速度一致，位置位于有效单元。
6. 非等距 XYZ 网格、非零映射原点、非均匀单位比例的 GPU / CPU 采样一致。
7. 平面数据保持 Y；输入含垂直分量时，GPU 保留原始速度作配色并约束运动，Long 同样可生成路径。
8. 并发文件加载采用最新结果；卸载取消在途解析。快速替换、卸载时旧 Long 任务不会重新写回显示。

## 截图来源

`Assets/Editor/VectorFieldGpuSmokeCheck.cs` 在 Unity Play 中等待数据和 GPU 初始化，调用真实相机渲染到 RenderTexture，然后 ReadPixels/EncodeToPNG。没有使用生成图片或后期绘图。README 两张图片不含编辑器窗口、路径或日志。

- `docs/images/long-mode.png`
- `docs/images/short-mode.png`

截图和 Play 验收后，资源清单检查发现 Short 材质留有一个不存在的旧 `_MainTex` GUID。当前粒子着色器不声明或读取该纹理；最后只将此失效引用清空并复查 GUID 闭合，没有为这一不影响渲染的清理重复运行画面验收。

## 复测

交互式：打开演示场景 → Play → 按 1/2，分别选择浅层/深层 → Stop/Show → 退出再进入 Play。自动验收可使用菜单 **Vector Field / Validate Play mode and capture README images**，它会切换场景并进入/退出 Play，覆盖 README 截图。

命令行（将编辑器位置和项目目录换为自己的位置；两次命令顺序执行，避免同一项目多开）：

```powershell
# $unityEditor 为本机 Unity.exe；$projectDirectory 为待验证项目目录。
& $unityEditor -batchmode -projectPath $projectDirectory -runTests -testPlatform EditMode -testResults editmode.xml -logFile editmode.log
& $unityEditor -batchmode -force-d3d11 -projectPath $projectDirectory -executeMethod VectorFieldGpuSmokeCheck.Run -logFile play-mode.log
python Tools/check_assets.py
```

第二条不要加 `-nographics`，否则不能验收 GPU；也不要加 `-quit`，验收工具会在完成后退出。原始日志/XML和额外截图仅保存在本地忽略目录中，不进入公开候选。

## 未验证与限制

- 没有进行独立 Player 构建、长时间运行、极限容量/帧率基准、其他 GPU、其他渲染管线或其他操作系统测试。
- 相机截图和变换断言不能证明所有负缩放、退化 Transform 或所有可见参数组合的表现；使用可逆、非零 Transform。
- 当前显示为静态场；时间变化网格、非结构网格、深度排序、极窄透明色带的密度估算见 README 限制。
- 干净副本仍需要合法安装的 Unity、正常包恢复环境和标准依赖；本次没有做断网重装或完全空白操作系统验收。
