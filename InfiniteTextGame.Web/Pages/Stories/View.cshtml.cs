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

        private const int _maxAutoWriteCount = 10;//����Զ�д���½���

        [BindProperty]
        public Story Story { get; set; }

        /// <summary>
        /// Ĭ���½��������ӵ�һ�½ڿ�ʼ��ÿ���½ڹ����Լ���Ĭ�Ϻ����½�
        /// </summary>
        [BindProperty]
        public IList<StoryChapter> DefaultChapterChains { get; set; }

        /// <summary>
        /// ��ǰ����ʹ�õ��Զ���д����
        /// </summary>
        [BindProperty]
        public WriterSpecific AutoWriterSpecific { get; set; }

        /// <summary>
        /// ʣ���Զ���д����
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

            //�������Ĭ���½�
            var currentChapter = Story.Chapters[0];
            DefaultChapterChains = new List<StoryChapter>() { currentChapter };
            while (currentChapter.DefaultNextChapter != null)
            {
                currentChapter = currentChapter.DefaultNextChapter;
                DefaultChapterChains.Add(currentChapter);
            }

            //��������Զ���дѡ����ȡ����¼
            if (TempData.ContainsKey("AutoWriterSpecific") && TempData.ContainsKey("RemainAutoWriteCount"))
            {
                AutoWriterSpecific = (WriterSpecific)TempData["AutoWriterSpecific"];
                RemainAutoWriteCount = (int)TempData["RemainAutoWriteCount"];
            }

            return Page();
        }

        /// <summary>
        /// ��ָ��ѡ���д��һ�½�
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        /// <exception cref="ApplicationException"></exception>
        public async Task<IActionResult> OnPostGenerateNextChapterAsync(Guid Id, int order)
        {
            if (Story == null)//����������������ã�Story�Ѽ��ؾͲ����ٴμ���
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

                //������Զ���д���¼����
                if (Enum.IsDefined(AutoWriterSpecific))
                    nextChapter.Specific = (int)AutoWriterSpecific;

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"generated new chapter {nextChapter.Title} of story {Story.Title} with {nextChapter.Content.Length} charactors, {nextChapter.PromptTokens} + {nextChapter.CompletionTokens} = {nextChapter.TotalTokens} tokens in {nextChapter.UseTime}ms");

                //�Զ���дδ���ʱ����ֵ����¼��TempData
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
        /// �Զ���дָ���½���
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

            //ʹ��ѡ�з�֧����������һ�½�
            return await OnPostGenerateNextChapterAsync(Id, chooseOptionOrder);
        }

        /// <summary>
        /// ɾ������
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
