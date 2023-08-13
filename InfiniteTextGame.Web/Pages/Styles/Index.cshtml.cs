using InfiniteTextGame.Lib;
using InfiniteTextGame.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InfiniteTextGame.Web.Pages.Styles
{
    public class IndexModel : PageModel
    {
        private readonly ILogger _logger;
        private readonly ITGDbContext _dbContext;

        [BindProperty]
        public IList<WritingStyle> Styles { get; set; }

        public IndexModel(ILogger<IndexModel> logger,
            ITGDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            Styles = await _dbContext.WritingStyles
                .OrderByDescending(s => s.CreateTime)
                .ToListAsync();
            return Page();
        }
    }
}
