using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfiniteTextGame.Models
{
    /// <summary>
    /// 故事章节实体类
    /// </summary>
    public class StoryChapter
    {
        public long Id { get; set; }
        /// <summary>
        /// 故事实体
        /// </summary>
        public Story Story { get; set; }
        /// <summary>
        /// 故事标题（仅用于json反序列化）
        /// </summary>
        [NotMapped]
        public string? StoryTitle { get; set; }
        /// <summary>
        /// 章节标题
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// 前情提要（迄今为止的故事梗概）
        /// </summary>
        public string? PreviousSummary { get; set; }
        /// <summary>
        /// 前一章节
        /// </summary>
        public StoryChapter? PreviousChapter { get; set; }
        /// <summary>
        /// 后续章节
        /// </summary>
        public IList<StoryChapter> NextChapters { get; set; }
        /// <summary>
        /// 默认后续章节
        /// </summary>
        public StoryChapter DefaultNextChapter
        {
            get
            {
                if (NextChapters == null || NextChapters.Count == 0) return null;
                return NextChapters.Last();
            }
        }
        /// <summary>
        /// 本章节来自上一章的选项序号
        /// </summary>
        public int PreviousOptionOrder { get; set; }
        /// <summary>
        /// 本章内容
        /// </summary>
        public string Content { get; set; }
        /// <summary>
        /// 后续选项列表
        /// </summary>
        public IList<StoryChapterOption> Options { get; set; }

        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get { return PromptTokens + CompletionTokens; } }
        /// <summary>
        /// 生成所用时间（ms）
        /// </summary>
        public long UseTime { get; set; }
        public DateTime CreateTime { get; set; }
        public bool Deleted { get; set; }
    }
}
