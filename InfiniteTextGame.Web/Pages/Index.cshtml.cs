using InfiniteTextGame.Lib;
using InfiniteTextGame.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace InfiniteTextGame.Web.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ITGDbContext _dbContext;

        private readonly int _recentStoryCount = 5;
        private readonly int _recentStyleCount = 5;

        [BindProperty]
        public IList<Story> RecentStories { get; set; }

        [BindProperty]
        public IList<WritingStyle> RecentStyles { get; set; }

        public IndexModel(ILogger<IndexModel> logger,
            ITGDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            RecentStories = await _dbContext.Stories
                .Include(s => s.Chapters)
                .OrderByDescending(s => s.UpdateTime)
                .Take(_recentStoryCount)
                .ToListAsync();
            RecentStyles = await _dbContext.WritingStyles
                .OrderByDescending(s => s.UpdateTime)
                .Take(_recentStyleCount)
                .ToListAsync();
            return Page();
        }
    }
}