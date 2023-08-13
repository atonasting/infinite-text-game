using InfiniteTextGame.Lib;
using InfiniteTextGame.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InfiniteTextGame.Web.Pages.Stories
{
    public class IndexModel : PageModel
    {
        private readonly ILogger _logger;
        private readonly ITGDbContext _dbContext;

        [BindProperty]
        public IList<Story> Stories { get; set; }

        public IndexModel(ILogger<IndexModel> logger, ITGDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            Stories = await _dbContext.Stories
                .Include(s => s.Chapters)
                .OrderByDescending(s => s.UpdateTime)
                .ToListAsync();

            return Page();
        }
    }
}
