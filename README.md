# 最终研究项目

## 项目描述

**ThreadsFall** 是一款基于 `Tetris` 的 *roguelike* 游戏，主题围绕网络 trolling、骂战和信息操纵。玩家不只是消除方块，也是在一个逐渐崩塌的网络帖子中努力保持冷静并做出判断。每一种俄罗斯方块都被重新解释为一种社交媒体行为，例如长篇观点、回音室、诱饵钩子、误导，或是把讨论带偏。每个 Boss 代表一种不同类型的网络对抗者，包括粉圈偶像、差评轰炸者、伪科学家、梗图群、匿名者和算法。玩家需要放置方块、消除行、管理 Attention、Composure 和 Noise，并在商店中花费 Attention 购买应对方式，例如截图、事实核查、静音、要求来源，或让讨论慢下来。

本项目的研究问题是：如何把网络 trolling 转化为一种可体验的游戏系统？更具体地说，我想探索网络空间中注意力、能动性和情绪是如何被操纵的。在制作过程中，我发现意义和可玩性是无法分开的。我的第一个原型过度依赖随机性和抽象隐喻，所以测试者很难真正投入。后来的版本中，主题被放进了方块、Boss、商店道具和状态系统里，因此玩家可以在游玩中感受到被误导、被打断，以及情绪上被逐渐消耗。

## 技术描述

项目使用 `Unity` 和 `C#` 开发，支持键盘和鼠标输入。主要系统包括俄罗斯方块棋盘控制器、回合流程控制器、单局属性、Boss 系统、商店系统，以及一套共享效果系统。Boss、商店道具、效果和美术参考都通过 ScriptableObjects 管理，这让内容调整更方便，不需要重写整个游戏流程。界面使用 `Unity UGUI`、`TextMesh Pro`、JSON 本地化和像素字体（Minecraft）。视觉素材主要使用 `Aseprite` 绘制，并导出为 PNG/GIF 序列。为了保持像素风格的清晰度，我使用了 `Pixel Perfect Camera`。

## 个人贡献

基本上，除了 Tetris 的基础框架借鉴自原项目之外，其他代码都重新做了一遍。因为我之前已经写过几款游戏，所以搭建过程并不算特别困难。系统和算法都是我自己设计的。（当然，原项目的代码也是我写的。）

之前的项目使用了 “thinking outside the box” 的想法。因为这次需要和研究问题结合起来，我重新组织了玩法和概念，让这些要求能够更自然地融合在一起。

## 项目图片

![Play](assets/final/1.jpg)

<sub>图 1 为了提高可访问性，项目使用几乎所有电脑用户都拥有的鼠标和键盘进行控制。</sub>

![Info1](assets/final/final.png)

<sub>图 2 最终项目运行时截图。</sub>

![Info2](assets/final/6.png)

<sub>图 3 悬停鼠标可以查看更多信息。</sub>

![Info3](assets/final/7.png)

<sub>图 4 商店页面。玩家可以购买带有特殊效果的道具。</sub>

![Info4](assets/final/8.png)

<sub>图 5 过高的 Noise 会导致文本变成乱码，表示信息流正在被噪音过度干扰。</sub>

## 演示视频

https://youtu.be/O8jCOPv9w6w?si=VO54_X4qXqyzQJ-5

## AI 工具记录

我在这个项目中使用了 Claude Code（agent）。这也是我第一次在集成开发环境中使用这类 agent，并因此了解了一些 MCP 相关知识。

AI 辅助完成的任务包括：

1. 在使用 UGUI 编写悬停提示代码时，我遇到了渲染缓存（或者说残影渲染）问题。我找不到任何和我的 Unity 版本匹配的在线答案，所以使用连接了 Unity Skills 的 Claude Code（当时 Fable 5 模型还可用）修复了这个问题。
   - 提示词：
    > 如截图所示，取消悬停后屏幕上仍然残留了一些文字，让画面变得很乱。请找出问题并给出解决方案。
   - 反馈：
    > **Ghost text**：这次找到了真正的原因：Pixel Perfect Camera 被设置成了 Windowbox crop（m_CropFrame: 3），它会在视图外形成黑边，而摄像机不会重新绘制这些区域。因此任何绘制在这些区域上的 tooltip，在取消悬停后都会一直残留在屏幕上。这正好对应了截图中的拖影模式（tooltip 标题、“ttention: 140”、描述文字碎片）。我在 MCP.unity 中把 crop mode 设置为 None，所以现在摄像机会在每一帧覆盖并清除整个窗口，消失的 UI 也会被正确擦除。（我一开始尝试过在 canvas 上加不透明背景，但 overlay canvas 会盖在世界画面上，导致棋盘变黑；所以删掉了它，crop 的修复才是正确方案。）
2. 占位内容生成：在我完成一个新功能后，我会让 agent 生成一些 JSON/SO 占位内容，用于开发阶段的临时可行性测试，这样我就不需要分心去写文本。具体包括：
   - 临时生成的 Boss 技能、商店道具和全局效果信息（从 ScriptableObject 模板生成的多种配置）
   - 使用 LLM 翻译的本地化键值对（简体中文和英文双语）
   - 在视觉素材完成前临时使用的 Image 组件占位图

![Ghost](assets/final/ghost.png)

<sub>图 6 Ghost Text（位于画面上方）。</sub>

## 用户测试

### 测试计划

简单来说，测试分为两轮：

第一轮是简单的原型测试，参与者是几位英国以外的游戏开发者。测试使用 FigJam 和屏幕共享进行概念讨论，以及一些基础可用性测试。测试目的，是在正式开发前讨论玩法的可行性，以及概念之间的结合程度。但结果并不理想，所以我调整了方向。

![PRB](assets/final/prb.png)

<sub>图 7 原型测试 FigJam 看板。</sub>

第二轮测试在开发完成后进行，参与者包括其他 CCI 课程的学生、来自 UAL 且有纯艺/电影背景的朋友，以及来自 UCL 和 Goldsmiths 的朋友。测试流程如下：

1. 简单说明操作方式：鼠标和键盘按键绑定；
2. 根据用户自己的意愿，安静观察用户游玩 3 分钟左右或更久；
3. 进行简短访谈，包括以下内容：
   1. 游戏经验、平时玩的游戏类型，以及是否玩过 Tetris/roguelike；
   2. 整体情绪评价，主要描述游玩感受；
   3. 对玩法的评价，主要关注 roguelike 元素、商店系统和 3D 属性系统；
   4. 对可用性和用户体验的评价，主要关注交互设计、信息传达和系统复杂度；
   5. 对概念和视觉的评价，主要关注文本表达是否被体验到，以及这种结合是否有效；
4. 回答用户在访谈中感到困惑的问题，并探索后续改进方向。

### 测试观察

![interview](assets/final/interview.jpg)

<sub>图 8 观察过程中的快速记录。</sub>

| 背景 | 游戏经验 | 总体反馈 | 玩法 | 用户体验 | 概念 |
|--------|-------|------|-------|-----|----|
| 机器人学 | Roguelike✓ Tetris×，整体一般 | 太长 |  | 悬停体验不好 | 呈现得比较完整 |
| 数字媒体 | Tetris 高手 | 有趣 | 太简单 | 悬停体验不好，本地化需要改进 | 无法专注于设定文本 |
| 电影 | Roguelike× Tetris✓ | 非常惊喜 | 新颖 | 不理解部分行为含义 | 不太关心设定文本 |
| 人类学 | Roguelike× Tetris✓ | 太复杂 | 挣扎 | 没有时间阅读 | 可以大致理解 |
| 平面设计 | 不喜欢玩游戏 | 干净清晰 | 什么都不懂但仍然玩得开心 | 游玩过程中读不完 | 需要停下来理解 |

<sub>表 1 五位测试者游玩后的访谈总结。</sub>

### 修改计划

我的下一步修改会首先关注经济系统和平衡性。当前 Attention 的收入公式增长太快，导致玩家可以买太多道具，从而降低了紧张感。我会降低增长系数，并让不同 Boss 对收入、Noise 和 Composure 产生更明显的差异化影响，使玩家必须在防御、信息核查和恢复之间做选择。

第二个重点是视觉反馈。隐藏预览、假预览、被污染的方块和故障旋转都需要更清晰的视觉或音频提示，让玩家理解这些是 Boss 行动，而不是输入错误或 bug。另外，我也需要重新思考如何让玩家在运行时不通过悬停 Boss 头像，也能意识到 Boss 的技能。

第三个重点是可读性。我会添加一个教程场景，或者一个收藏页面，让玩家可以在单局之外查看 Boss、道具和方块隐喻。

最后，我会继续改进英文/中文本地化，并加入色盲模式等可访问性选项。

## Lab 链接

1. [游戏原型](Week1.md)：创建游戏原型和快速 play-test 的方法（用于早期开发）；
2. [`ScriptableObjects`](Week3.md)：使用这个功能可以基于模板快速创建相似对象；只需要把它们拖进 inspector 的插槽，就能创建大量实例；
3. [`OOP`](Week4.md)：一些关于使用面向对象编程语言的反思，也巩固了我的相关知识和实践技能。

## Sprint 链接

- [Sprint 1](Sprint1.md)：Triple Tetris
- [Sprint 2](Sprint2.md)：Aquatic Ecosystem

## Pecha-Kucha 幻灯片

[幻灯片](assets/final/slides.pptx)

## 参考文献列表

- Marzęda, A. (2025). Diversity and complexity of online trolling: an extended classification and analysis of social consequences. Media Biznes Kultura, 19(2), 59-96. https://doi.org/10.4467/25442554.MBK.25.018.22867.
- Bąkowicz, K. (2024). Trolle jako kreatorzy dezinformacji. Analiza zjawiska. Media Biznes Kultura, 1(16), 7-18. https://doi.org/10.4467/25442554.MBK.24.001.19938.
- Lai, S., & Shen, Y. (2023). The Influence of Perceived Trolling on Weibo Users Lurking Behavior The Moderating Role of Social Media Affordance and Platform Engagement. Communications in Humanities Research, 22(1), 81-87. https://doi.org/10.54254/2753-7064/22/20231601.
- Sun, Q., & Shen, C. (2021). Who would respond to A troll? A social network analysis of reactions to trolls in online communities. Computers in Human Behavior, 121, 106786. https://doi.org/10.1016/j.chb.2021.106786.
- Anthropic (2026). Claude Code (Fable 5 Model), 08/06.
