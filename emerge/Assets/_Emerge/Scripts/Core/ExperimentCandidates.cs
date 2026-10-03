using System.Collections.Generic;
using Emerge.Cells;
using UnityEngine;

namespace Emerge.Core
{
    public static class ExperimentCandidates
    {
        private static ExperimentCell Cell(CellKind kind, float x, float y, float angle = 0) => new ExperimentCell { kind = kind, position = new Vector2(x, y), rotation = angle };
        private static ExperimentLink Link(int a, int b, IntentChannel channel = IntentChannel.W) => new ExperimentLink { a = a, b = b, channel = channel };
        public static ExperimentData[] Defaults()
        {
            var tail = new ExperimentData { id = "swing-tail", title = "收缩摆尾候选", idea = "W 保持核心直连纤毛推进，A 激活收缩臂和末端纤毛，观察姿态与轨迹。关闭收缩响应可保留同样纤毛输入作对照。", controls = "Tab 游动；W 直连推进，W+A 驱动摆尾；松 A 复原。", nutrients = 8, energy = 10,
                cells = new[] { Cell(CellKind.Core, -1, -1), Cell(CellKind.Cilia, -1, 0.3f, 180), Cell(CellKind.Contractor, 0.42f, -1), Cell(CellKind.Cilia, 1.7f, -1, 90), Cell(CellKind.Absorber, -2.37f, -1) },
                links = new[] { Link(0, 1), Link(0, 2, IntentChannel.A), Link(2, 3), Link(0, 4) } };
            var arms = new ExperimentData { id = "telescoping-arms", title = "收缩可变推进臂", idea = "对称收缩臂携带两枚同向纤毛，缩短臂长是否改变转动和运动轨迹？关闭收缩响应比较相同纤毛输入。", controls = "Tab；W 同时收缩与推水，松 W 复原；6 查看局部流向。", nutrients = 8, energy = 10,
                cells = new[] { Cell(CellKind.Core, 0, -1), Cell(CellKind.Contractor, 0, 0.42f), Cell(CellKind.Contractor, 0, -2.42f), Cell(CellKind.Cilia, 0, 1.7f, 180), Cell(CellKind.Cilia, 0, -3.7f, 180), Cell(CellKind.Absorber, 1.37f, -1) },
                links = new[] { Link(0, 1), Link(0, 2), Link(1, 3), Link(2, 4), Link(0, 5) } };
            var mouth = new ExperimentData { id = "contractile-mouth", title = "收缩膜口导流摄食", idea = "两侧收缩细胞移动斜膜，尝试改变开口和留存；环境来流提供营养输送。没有把收缩动作当作水泵。", controls = "Tab；A 按住/松开改变膜口；比较关闭收缩响应及无来流版本。", nutrients = 2, energy = 8,
                cells = new[] { Cell(CellKind.Core, 0, -2.37f), Cell(CellKind.Absorber, 0, -1), Cell(CellKind.Contractor, -1.35f, -1), Cell(CellKind.Contractor, 1.35f, -1), Cell(CellKind.Membrane, -1.35f, 0.48f, 45), Cell(CellKind.Membrane, 1.35f, 0.48f, -45) },
                links = new[] { Link(0, 1, IntentChannel.A), Link(1, 2), Link(1, 3), Link(2, 4), Link(3, 5) },
                flows = new[] { new ExperimentFlow { position = new Vector2(0, -1), rotation = -90, speed = 0.45f } } };
            var mouthFood = new List<ExperimentFood>();
            for (int row = 0; row < 4; row++) for (int col = 0; col < 5; col++) mouthFood.Add(new ExperimentFood { position = new Vector2((col - 2) * 0.4f, 1.5f + row * 0.23f) });
            mouth.food = mouthFood.ToArray();
            var noFlow = JsonUtility.FromJson<ExperimentData>(JsonUtility.ToJson(mouth));
            noFlow.id = "mouth-no-flow"; noFlow.title = "膜口无来流对照"; noFlow.flows = new ExperimentFlow[0];
            noFlow.idea = "相同膜口和食物位置，取消环境来流，检验收缩是否只能改变结构。没有摄食也是有效结果。";
            return new[] { tail, arms, mouth, noFlow, Rotary("spiral", "360°旋转吸入候选", 135),
                Rotary("tangential", "纯切向旋流对照", 90), Rotary("radial", "径向吸入对照", 180) };
        }
        private static ExperimentData Rotary(string id, string title, float offset)
        {
            var cells = new List<ExperimentCell> { Cell(CellKind.Core, 0, -1) };
            var links = new List<ExperimentLink>(); var food = new List<ExperimentFood>();
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120 * Mathf.Deg2Rad; Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 p = new Vector2(0, -1) + direction * 1.37f;
                cells.Add(Cell(CellKind.Absorber, p.x, p.y)); int absorber = cells.Count - 1;
                p = new Vector2(0, -1) + direction * 2.6f;
                cells.Add(Cell(CellKind.Cilia, p.x, p.y, i * 120 + offset));
                links.Add(Link(0, absorber)); links.Add(Link(absorber, cells.Count - 1));
            }
            for (int ring = 0; ring < 2; ring++) for (int i = 0; i < 16; i++)
            {
                float a = (i + 0.5f) * Mathf.PI / 8;
                food.Add(new ExperimentFood { position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (3 + ring * 0.25f) + new Vector2(0, -1) });
            }
            return new ExperimentData { id = id, title = title, nutrients = 8, energy = 10, cells = cells.ToArray(), links = links.ToArray(), food = food.ToArray(),
                controls = "Tab；按住 W 观察旋转、环形食物路径、捕获与供能。分别载入三种朝向对照；6 切换水流。",
                idea = offset == 135 ? "三臂纤毛具有切向与向内分量，身体反向旋转。验证环周食物是否旋转靠近吸收面；缺能会降低转速。" :
                    offset == 90 ? "只有切向推水：比较是否绕圈却不能吸入。所有其他初始条件相同。" : "纯径向向内推水：比较不依赖旋转的摄食。所有其他初始条件相同。" };
        }
    }
}
