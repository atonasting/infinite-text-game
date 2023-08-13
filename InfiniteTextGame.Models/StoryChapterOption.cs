using System;
using System.Collections.Generic;
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
    }
}
