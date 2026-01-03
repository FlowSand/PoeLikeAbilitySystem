你正在为一个 Unity PoE-like 技能系统实现 GraphIR（中间表示层）。

GraphIR 是 Effect Graph 的“作者态 → 运行时”的唯一桥梁。

【目标】
实现 GraphIR 的数据结构定义 + 静态校验器。

【强制约束】
- GraphIR 必须是“纯数据结构”
- 不允许依赖 NodeGraphProcessor
- 可被 Editor 与 Runtime 共享
- 放在 Assets/Scripts/Combat.Runtime/GraphIR/
- 禁止 UnityEditor API

【需要实现的核心结构】

1. GraphIR
- graphId
- version
- List<IRNode>
- List<IREdge>
- entryNodeId

2. IRNode
- nodeId
- nodeType（enum）
- Dictionary<string, IRPort>

3. IRPort
- portName
- portType（enum）
- direction（In / Out）

4. IREdge
- fromNodeId
- fromPort
- toNodeId
- toPort

【端口类型枚举（最小集）】
- Number
- Bool
- EntityId
- EntityList
- DamageSpec

【必须实现的静态校验】
- entryNodeId 存在
- nodeId 唯一
- Edge 引用合法
- 端口类型匹配
- 默认禁止环（DAG）

提供：
- GraphIRValidator
    - Validate(GraphIR) → ValidationResult

【必须提供的测试】
- 合法 GraphIR → 校验通过
- 非法 Edge → 返回错误信息
- 类型不匹配 → 校验失败

【交付要求】
- 清晰的错误信息（nodeId + 原因）
- 给出一个最小合法 GraphIR 示例（代码或 JSON）
