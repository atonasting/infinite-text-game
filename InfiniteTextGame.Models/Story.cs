namespace InfiniteTextGame.Models
{
    /// <summary>
    /// 故事实体类
    /// </summary>
    public class Story
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        /// <summary>
        /// 故事标题
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// 风格提示
        /// </summary>
        public string StylePrompt { get; set; }
        /// <summary>
        /// 使用的模型名称
        /// </summary>
        public string Model { get; set; }
        /// <summary>
        /// 章节列表
        /// </summary>
        public IList<StoryChapter> Chapters { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }
        /// <summary>
        /// 是否公开
        /// </summary>
        public bool IsPublic { get; set; }
        /// <summary>
        /// 是否已关闭（不再允许编写剧情）
        /// </summary>
        public bool Closed { get; set; }

    }
}