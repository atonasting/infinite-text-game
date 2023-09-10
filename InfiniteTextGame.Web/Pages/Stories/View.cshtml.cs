using InfiniteTextGame.Lib;
using InfiniteTextGame.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InfiniteTextGame.Web.Pages.Stories
{
    public class ViewModel : PageModel
    {
        private readonly ILogger _logger;
        private readonly AIService _aiService;
        private readonly ITGDbContext _dbContext;
        private readonly AutoWriteService _autoWriteService;

        private const int _maxAutoWriteCount = 10;//最大自动写作章节数

        [BindProperty]
        public Story Story { get; set; }

        /// <summary>
        /// 默认章节链，即从第一章节开始，每个章节关联自己的默认后续章节
        /// </summary>
        [BindProperty]
        public IList<StoryChapter> DefaultChapterChains { get; set; }

        /// <summary>
        /// 当前正在使用的自动编写特性
        /// </summary>
        [BindProperty]
        public WriterSpecific AutoWriterSpecific { get; set; }

        /// <summary>
        /// 剩余自动编写数量
        /// </summary>
        [BindProperty]
        public int RemainAutoWriteCount { get; set; }

        public ViewModel(ILogger<ViewModel> logger,
            AIService aIService,
            ITGDbContext dbContext,
            AutoWriteService autoWriteService)
        {
            _logger = logger;
            _aiService = aIService;
            _dbContext = dbContext;
            _autoWriteService = autoWriteService;
        }

        public async Task<IActionResult> OnGetAsync(Guid Id)
        {
            Story = await _dbContext.Stories
                .AsSplitQuery()
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.PreviousChapter)
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.NextChapters)
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.Options)
                .SingleOrDefaultAsync(s => s.Id == Id);
            if (Story == null) { return NotFound(); }

            //逐个加载默认章节
            var currentChapter = Story.Chapters[0];
            DefaultChapterChains = new List<StoryChapter>() { currentChapter };
            while (currentChapter.DefaultNextChapter != null)
            {
                currentChapter = currentChapter.DefaultNextChapter;
                DefaultChapterChains.Add(currentChapter);
            }

            //如果存在自动编写选项，则读取并记录
            if (TempData.ContainsKey("AutoWriterSpecific") && TempData.ContainsKey("RemainAutoWriteCount"))
            {
                AutoWriterSpecific = (WriterSpecific)TempData["AutoWriterSpecific"];
                RemainAutoWriteCount = (int)TempData["RemainAutoWriteCount"];
            }

            return Page();
        }

        /// <summary>
        /// 按指定选项编写下一章节
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        /// <exception cref="ApplicationException"></exception>
        public async Task<IActionResult> OnPostGenerateNextChapterAsync(Guid Id, int order)
        {
            if (Story == null)//如果被其他方法调用，Story已加载就不必再次加载
            {
                Story = await _dbContext.Stories
                    .AsSplitQuery()
                    .Include(s => s.Chapters)
                        .ThenInclude(c => c.PreviousChapter)
                    .Include(s => s.Chapters)
                        .ThenInclude(c => c.NextChapters)
                    .Include(s => s.Chapters)
                        .ThenInclude(c => c.Options)
                    .SingleOrDefaultAsync(s => s.Id == Id);
                if (Story == null) { return NotFound(); }
            }

            var lastChapter = Story.Chapters.Last();
            if (lastChapter.Options.SingleOrDefault(o => o.Order == order) == null)
            {
                _logger.LogWarning($"order {order} not exist");
                return BadRequest($"order {order} not exist");
            }

            try
            {
                _logger.LogInformation($"try generate new chapter of story {Story.Title}");
                var nextChapter = await _aiService.GenerateNextChapter(lastChapter, order);

                //如果是自动编写则记录作者
                if (Enum.IsDefined(AutoWriterSpecific))
                    nextChapter.Specific = (int)AutoWriterSpecific;

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"generated new chapter {nextChapter.Title} of story {Story.Title} with {nextChapter.Content.Length} charactors, {nextChapter.PromptTokens} + {nextChapter.CompletionTokens} = {nextChapter.TotalTokens} tokens in {nextChapter.UseTime}ms");

                //自动编写未完成时，赋值并记录到TempData
                if (RemainAutoWriteCount > 1)
                {
                    RemainAutoWriteCount--;
                    TempData["AutoWriterSpecific"] = AutoWriterSpecific;
                    TempData["RemainAutoWriteCount"] = RemainAutoWriteCount;
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("generate new chapter error", ex);
            }

            return StatusCode(200);
        }

        /// <summary>
        /// 自动编写指定章节数
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="specific"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <exception cref="ApplicationException"></exception>
        public async Task<IActionResult> OnPostAutoWriteAsync(Guid Id, int specific, int count)
        {
            if (count <= 0 || count > _maxAutoWriteCount)
                return BadRequest($"invalid count {count}");

            if (!Enum.IsDefined(typeof(WriterSpecific), specific))
                return BadRequest($"undefined specific {specific}");

            AutoWriterSpecific = (WriterSpecific)specific;
            RemainAutoWriteCount = count;

            Story = await _dbContext.Stories
                .AsSplitQuery()
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.PreviousChapter)
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.NextChapters)
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.Options)
                .SingleOrDefaultAsync(s => s.Id == Id);
            if (Story == null) { return NotFound(); }

            var lastChapter = Story.Chapters.Last();
            var chooseOptionOrder = _autoWriteService.ChooseOption(AutoWriterSpecific, lastChapter.Options.ToList());

            _logger.LogInformation($"auto write story {Story.Title} with {AutoWriterSpecific.GetDescription()}, choose option {chooseOptionOrder}");

            //使用选中分支剧情生成下一章节
            return await OnPostGenerateNextChapterAsync(Id, chooseOptionOrder);
        }

        /// <summary>
        /// 删除故事
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPostRemoveStoryAsync(Guid Id)
        {
            Story = await _dbContext.Stories
                .Include(s => s.Chapters)
                .SingleOrDefaultAsync(s => s.Id == Id);
            if (Story == null) { return NotFound(); }

            _dbContext.Stories.Remove(Story);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"story {Story.Title} with {Story.Chapters.Count} chapters (Model {Story.Model}) is removed");
            return RedirectToPage("Index");
        }
    }
}
