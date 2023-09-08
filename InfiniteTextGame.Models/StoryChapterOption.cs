using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfiniteTextGame.Models
{
    /// <summary>
    /// 故事章节选项
    /// </summary>
    public class StoryChapterOption
    {
        public long Id { get; set; }
        public StoryChapter Chapter { get; set; }
        /// <summary>
        /// 是否为“继续”选项（无分支）
        /// </summary>
        public bool IsContinue { get; set; }
        /// <summary>
        /// 序号
        /// </summary>
        public int Order { get; set; }
        /// <summary>
        /// 选项名称
        /// </summary>
        public string? Name { get; set; }
        /// <summary>
        /// 选项描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 正面程度分数(1~5)
        /// </summary>
        public int PositivityScore { get; set; }
        /// <summary>
        /// 影响规模分数(1~5)
        /// </summary>
        public int ImpactScore { get; set; }
        /// <summary>
        /// 复杂程度分数(1~5)
        /// </summary>
        public int ComplexityScore { get; set; }

        /// <summary>
        /// 正面程度分数(1~5，json解析用）
        /// </summary>
        [NotMapped]
        public string? PositivityScoreStr { get; set; }
        /// <summary>
        /// 影响规模分数(1~5，json解析用）
        /// </summary>
        [NotMapped]
        public string? ImpactScoreStr { get; set; }
        /// <summary>
        /// 复杂程度分数(1~5，json解析用）
        /// </summary>
        [NotMapped]
        public string? ComplexityScoreStr { get; set; }

        /// <summary>
        /// 正面程度图标
        /// </summary>
        public string PositivityIcon
        {
            get
            {
                return PositivityScore switch
                {
                    1 or 2 => "far fa-frown",
                    3 => "far fa-meh",
                    4 or 5 => "far fa-smile",
                    _ => "fas fa-circle-exclamation"
                };
            }
        }

        /// <summary>
        /// 影响规模图标
        /// </summary>
        public string ImpactIcon
        {
            get
            {
                return ImpactScore switch
                {
                    1 or 2 => "fas fa-volume-off",
                    3 => "fas fa-volume-low",
                    4 or 5 => "fas fa-volume-high",
                    _ => "fas fas fa-circle-exclamation"
                };
            }
        }

        /// <summary>
        /// 复杂程度图标
        /// </summary>
        public string ComplexityIcon
        {
            get
            {
                return ComplexityScore switch
                {
                    1 or 2 => "fas fa-circle-notch",
                    3 => "fas fa-cog",
                    4 or 5 => "fas fa-cogs",
                    _ => "fas fa-circle-exclamation"
                };
            }
        }
    }
}
