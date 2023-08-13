using InfiniteTextGame.Lib;
using InfiniteTextGame.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata.Ecma335;

namespace InfiniteTextGame.Web.Pages.Stories
{
    public class NewModel : PageModel
    {
        private readonly ILogger _logger;
        private readonly IAIClient _aiClient;
        private readonly ITGDbContext _dbContext;

        [BindProperty]
        public IList<WritingStyle> Styles { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "请选择写作风格！")]
        public int SelectStyleId { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "请选择模型！")]
        public string SelectModel { get; set; }

        public NewModel(ILogger<NewModel> logger,
            IAIClient aiClient,
            ITGDbContext dbContext)
        {
            _logger = logger;
            _aiClient = aiClient;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            Styles = await _dbContext.WritingStyles
                .OrderByDescending(s => s.CreateTime)
                .ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return BadRequest();

            var selectStyle = await _dbContext.WritingStyles
                .SingleOrDefaultAsync(s => s.Id == SelectStyleId);

            if (selectStyle == null) return BadRequest($"no writing style id {SelectStyleId}");

            _logger.LogInformation($"try generate new story in style {selectStyle.Name} ({SelectModel})");

            var story = await _aiClient.GenerateStory(selectStyle, SelectModel);
            _dbContext.Stories.Add(story);
            selectStyle.UseTimes++;
            await _dbContext.SaveChangesAsync();

            var firstChapter = story.Chapters.FirstOrDefault();
            _logger.LogInformation($"generated new story {story.Title} in style {selectStyle.Name} ({SelectModel})\nfirst chapter {firstChapter.Title} with {firstChapter.Content.Length} charactors, {firstChapter.PromptTokens} + {firstChapter.CompletionTokens} = {firstChapter.TotalTokens} tokens in {firstChapter.UseTime}ms");

            return RedirectToPage("/Stories/View", new {story.Id});
        }
    }
}
