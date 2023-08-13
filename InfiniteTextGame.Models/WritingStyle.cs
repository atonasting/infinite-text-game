using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfiniteTextGame.Models
{
    /// <summary>
    /// 写作风格
    /// </summary>
    public class WritingStyle
    {
        public long Id { get; set; }
        /// <summary>
        /// 风格名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 生成风格所用的原始文章
        /// </summary>
        public string? Source { get; set; }
        /// <summary>
        /// 风格关键词（用逗号分隔）
        /// </summary>
        public string KeyWords { get; set; }
        /// <summary>
        /// 风格关键词liebiao （仅用于json反序列化）
        /// </summary>
        [NotMapped]
        public IList<string> KeyWordList { get; set; }
        /// <summary>
        /// 风格被使用次数
        /// </summary>
        public int UseTimes { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }
    }
}
