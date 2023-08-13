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
        private readonly IAIClient _aiClient;
        private readonly ITGDbContext _dbContext;

        [ModelBinder]
        public Story Story { get; set; }

        /// <summary>
        /// 默认章节链，即从第一章节开始，每个章节关联自己的默认后续章节
        /// </summary>
        [ModelBinder]
        public IList<StoryChapter> DefaultChapterChains { get; set; }

        public ViewModel(ILogger<ViewModel> logger,
            IAIClient aiClient,
            ITGDbContext dbContext)
        {
            _logger = logger;
            _aiClient = aiClient;
            _dbContext = dbContext;
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

            return Page();
        }
        public async Task<IActionResult> OnPostGenerateNextChapterAsync(Guid Id, int Order)
        {
            Story = await _dbContext.Stories
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.PreviousChapter)
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.NextChapters)
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.Options)
                .SingleOrDefaultAsync(s => s.Id == Id);
            if (Story == null) { return NotFound(); }

            var lastChapter = Story.Chapters.Last();
            if (lastChapter.Options.SingleOrDefault(o => o.Order == Order) == null)
            {
                _logger.LogWarning($"order {Order} not exist");
                return BadRequest($"order {Order} not exist");
            }

            var nextChapter = await _aiClient.GenerateNextChapter(lastChapter, Order);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"generate new chapter {nextChapter.Title} with {nextChapter.Content.Length} charactors, {nextChapter.PromptTokens} + {nextChapter.CompletionTokens} = {nextChapter.TotalTokens} tokens in {nextChapter.UseTime}ms");
            return StatusCode(200);
        }

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
