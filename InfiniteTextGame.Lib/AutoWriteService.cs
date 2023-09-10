using InfiniteTextGame.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace InfiniteTextGame.Lib
{
    /// <summary>
    /// 自动写作服务
    /// </summary>
    public class AutoWriteService
    {
        private ILogger _logger;
        private Dictionary<WriterSpecific, SpecificWeights> _weights;

        public AutoWriteService(ILogger<AutoWriteService> logger)
        {
            _logger = logger;
            //为每种性格初始化权重
            _weights = new Dictionary<WriterSpecific, SpecificWeights>
            {
                { WriterSpecific.Dramatic, new SpecificWeights(0, 0.9, 0.8) },
                { WriterSpecific.Simplistic, new SpecificWeights(0.9, -0.5, -0.8) },
                { WriterSpecific.Darkness, new SpecificWeights(-0.9, 0.7, 0.6) },
                { WriterSpecific.Intricate, new SpecificWeights(0.2, -0.3, 1) },
                { WriterSpecific.Neutral, new SpecificWeights(1, 1, 1) },
            };
        }

        /// <summary>
        /// 根据作者性格和选项权重自动选择最高的选项
        /// </summary>
        /// <param name="specificId"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public int ChooseOption(WriterSpecific specific, List<StoryChapterOption> options)
        {
            var weight = _weights[specific];
            var bestOption = options.OrderByDescending(
                o => o.PositivityScore * weight.Positivity
                    + o.ImpactScore * weight.Impact
                    + o.ComplexityScore * weight.Complexity)
                .FirstOrDefault();
            return bestOption.Order;
        }
    }

    /// <summary>
    /// 作者性格枚举
    /// </summary>
    public enum WriterSpecific
    {
        [Description("戏剧化")]
        Dramatic = 1,
        [Description("单纯型")]
        Simplistic = 2,
        [Description("黑暗系")]
        Darkness = 3,
        [Description("烧脑派")]
        Intricate = 4,
        [Description("中立党")]
        Neutral = 5,
    }

    /// <summary>
    /// 每种性格对维度影响的权重
    /// </summary>
    public class SpecificWeights
    {
        /// <summary>
        /// 正面程度(-1~1)
        /// </summary>
        public double Positivity;
        /// <summary>
        /// 影响力(-1~1)
        /// </summary>
        public double Impact;
        /// <summary>
        /// 复杂程度(-1~1)
        /// </summary>
        public double Complexity;

        public SpecificWeights(double positivity, double impact, double complexity)
        {
            Positivity = positivity;
            Impact = impact;
            Complexity = complexity;
        }
    }
}
