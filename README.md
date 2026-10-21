# Unity 通用矢量场可视化

可嵌入普通 Unity 场景的局部 XYZ 矢量场显示模块。Long 使用双向 RK4 生成流线并沿路径显示渐隐轨迹；Short 使用 Compute Shader 更新按流向排列的短粒子。数据、色带、显示速度、数量和 GPU 资源按实例独立管理。

| Long：长流线 | Short：短粒子 |
| --- | --- |
| ![Long 模式](docs/images/long-mode.png) | ![Short 模式](docs/images/short-mode.png) |

## 主要控制项

| 参数 | 含义 |
| --- | --- |
| `mode` | Long / Short；没有强制 Short 的开关 |
| `lineWidth / trailLength` | Long 局部宽度 / 可见拖尾步数 |
| `stepSize / maxSteps` | Long 局部弧长步距 / 双向积分总步数上限 |
| `maxStreamlines / minSpacing` | Long 条数上限 / 种子局部间距；改后调用 `Long.Rebuild()` |
| `particleScale / baseAspect / stretchMult` | Short 局部尺寸 / 基础长宽比 / 按物理速度拉伸 |
| `streamlineSpeed / speedMultiplier` | 两模式显示倍率，默认 1；0 暂停运动 |
| `minColorSpeed / maxColorSpeed` | 显式色标范围；上限不大于下限时使用数据最大物理速度乘色标单位倍率 |
| `automaticParticleCount` | Short 自动 / 固定分配 |
| `mainParticleCount × fixedParticleCountUnit` | 固定模式申请数量；0 为不分配，不代表自动 |
| `automaticReferenceSize` | 自动模式参考尺寸，局部显示单位；平面模式只使用 XZ 面积 |
| `automaticReferenceParticleCount` | 该参考面积/体积对应的数量，独立可调 |
| `automaticParticleLimit` | 两种数量策略的分配上限，0 为不分配 |

自动数量为 `round(参考数量 × 估算可见面积或体积 / 参考面积或体积)`，非空时至少 1，再受上限约束。出生单元按面积/体积抽样，单元内拒绝缺测、静止、低于可见阈值和透明色带位置，不按速度加权增加密度。可见度参数或数量配置改变时重建分配；动画速度和尺寸可直接调整。

## 验证与限制

实际结果、复测命令和截图来源见 [验证记录](docs/VALIDATION.md)。不提供未经测量的帧率或容量承诺。

- 只处理静态、轴对齐的直角网格；非结构网格和随时间变化的数据需外部重采样/更新。
- Long 是有限长度的静态流线及沿线动画，不是严格时间积分的拉格朗日轨迹；开放路径头部会重新从起点开始。显示步进有离散误差。
- 自动可见面积/体积是每单元 4/8 点求积估算；极窄可见速度带可能估计不足。Short 每次最多尝试 32 个出生位置，失败者隐藏并于后续帧重试。
- 透明粒子不做逐粒子深度排序；没有沙盘雾化背景层或跨管线适配。大网格受显存和 Texture3D 维度限制；本次不宣称极限性能。
- 原型的旧场景、原始数据和设计记录保存在本地忽略目录中，不在发布候选内。示例菜单生成新场景，不恢复旧业务演示。
- 项目代码及本仓库自有示例采用 [MIT 许可证](LICENSE)。Unity 与包依赖遵循其各自许可证；MIT 不替代这些第三方条款。

发布快照的范围见 [公开准备说明](docs/PUBLICATION.md) 和根目录 `PUBLISH_FILES.txt`。原型的 Git 历史与原始数据不属于此发布快照。
