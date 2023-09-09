using InfiniteTextGame.Lib;
using InfiniteTextGame.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace InfiniteTextGame.Web.Pages.Styles
{
    public class EditModel : PageModel
    {
        private readonly ILogger _logger;
        private readonly AIService _aiService;
        private readonly ITGDbContext _dbContext;

        [BindProperty]
        public string? Source { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "请填写风格名称")]
        public string Name { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "关键词不能为空")]
        public string KeyWords { get; set; }

        public EditModel(ILogger<EditModel> logger,
            AIService aiService,
            ITGDbContext dbContext)
        {
            _logger = logger;
            _aiService = aiService;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> OnGetAsync(int? Id = null)
        {
            if (Id.HasValue)
            {
                var style = await _dbContext.WritingStyles.
                    SingleOrDefaultAsync(s => s.Id == Id);
                if (style == null) { return NotFound(); }
                Source = style.Source;
                Name = style.Name;
                KeyWords = style.KeyWords;
            }
            return Page();
        }

        /// <summary>
        /// 创建或修改风格
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPostAsync(int? Id)
        {
            if (!ModelState.IsValid) return BadRequest();

            WritingStyle style;

            if (Id.HasValue)
            {
                style = await _dbContext.WritingStyles.
                    SingleOrDefaultAsync(s => s.Id == Id);
                if (style == null) { return NotFound(); }
            }
            else
            {
                style = new WritingStyle() { CreateTime = DateTime.UtcNow };
                _dbContext.WritingStyles.Add(style);
            }
            style.Source = Source;
            style.Name = Name;
            style.KeyWords = KeyWords;
            style.UpdateTime = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"save writing style {style.Name}, keywords:\n{style.KeyWords}");

            return RedirectToPage("Index");
        }

        /// <summary>
        /// 根据原文生成风格名称和关键词
        /// </summary>
        /// <param name="Source"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPostGenerateKeywordsAsync(string Source)
        {
            if (Source == null) { return BadRequest("no source"); }
            var style = await _aiService.GenerateWritingStyle(Source);
            _logger.LogInformation($"generate writing style from source ({Source.Length} characters) to:\nname: {style.Name}\nkeywords:{style.KeyWords}");

            return new JsonResult(new Dictionary<string, string>()
            {
                { "name", style.Name },
                { "keywords", style.KeyWords },
            });
        }

        /// <summary>
        /// 删除风格
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPostRemoveAsync(int Id)
        {
            var style = await _dbContext.WritingStyles.
                SingleOrDefaultAsync(s => s.Id == Id);
            if (style == null) { return NotFound(); }

            _dbContext.WritingStyles.Remove(style);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"writing style {style.Name} removed.");
            return RedirectToPage("Index");
        }
    }
}