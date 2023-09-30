using OpenAI.Managers;
using OpenAI;
using OpenAI.Builders;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.SharedModels;
using System.Text.Json;
using InfiniteTextGame.Models;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace InfiniteTextGame.Lib
{
    /// <summary>
    /// AI服务
    /// </summary>
    public class AIService
    {
        private ILogger _logger;
        private OpenAIService _sdk;
        private ProviderType _providerType;
        private string _modelName;//用于记录的模型名

        private readonly string _language = "Chinese";
        private readonly int _optionsCount = 4;//选项数量
        private readonly int _titleMaxLength = 4;//默认标题最大单词数
        private readonly int _optionNameMaxLength = 4;//默认选项名最大单词数
        private readonly int _optionDescriptionMaxLength = 8;//默认选项描述最大单词数
        private readonly int _chapterLength = 500;//默认每段单词数
        private readonly int _previousSummaryLength = 200;//默认前情提要单词数

        private readonly int _autoRetryMaxCount = 5;//编写出错时最大重试次数

        private readonly JsonSerializerOptions _defaultJsonSerializerOptions = new JsonSerializerOptions()
        {
            AllowTrailingCommas = true,
        };

        /// <summary>
        /// OpenAI构造函数
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="proxy"></param>
        public AIService(string apiKey, string? defaultModel, string? proxy, ILogger<AIService> logger)
        {
            var httpClientFactory = new HttpClientFactoryWithProxy(proxy);
            _providerType = ProviderType.OpenAi;

            _sdk = new OpenAIService(new OpenAiOptions()
            {
                ProviderType = _providerType,
                ApiKey = apiKey,
                DefaultModelId = defaultModel ?? OpenAI.ObjectModels.Models.Gpt_3_5_Turbo_0613
            },
            httpClientFactory.CreateClient());
            _modelName = $"OpenAI:{_sdk.GetDefaultModelId()}";
            _logger = logger;
        }

        /// <summary>
        /// Azure构造函数
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="proxy"></param>
        public AIService(string apiKey, string resourceName, string deploymentId, string? proxy, ILogger<AIService> logger)
        {
            var httpClientFactory = new HttpClientFactoryWithProxy(proxy);
            _providerType = ProviderType.Azure;

            _sdk = new OpenAIService(new OpenAiOptions()
            {
                ProviderType = _providerType,
                ApiKey = apiKey,
                ResourceName = resourceName,
                DeploymentId = deploymentId,
                DefaultModelId = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo_0613,
                ApiVersion = "2023-07-01-preview"
            },
            httpClientFactory.CreateClient());
            _modelName = $"Azure:{deploymentId}";
            _logger = logger;
        }

        /// <summary>
        /// 生成故事以及第一章
        /// </summary>
        /// <returns></returns>
        public async Task<Story> GenerateStory(WritingStyle style)
        {
            var messages = new List<ChatMessage> {
                ChatMessage.FromSystem($"You are a writer, working on a lengthy story.\nThe overall style of the story is as follows: {style.KeyWords}"),
                ChatMessage.FromUser($"Start by writing the first chapter, setting the scene and introducing the main characters, with detailed descriptions. Suggested length is {_chapterLength} words."),
                ChatMessage.FromSystem($"You must respond in {_language}."),
        };

            var chapterFunc = new FunctionDefinitionBuilder("chapter", "Chapter content")
                .AddParameter("StoryTitle", PropertyDefinition.DefineString($"Story title, not exceeding {_titleMaxLength} words"))
                .AddParameter("Title", PropertyDefinition.DefineString($"This chapter's title, not exceeding {_titleMaxLength} words. Do not include labels like \"Chapter One\"."))
                .AddParameter("Content", PropertyDefinition.DefineString($"Content for this chapter, suggested length is {_chapterLength} words."))
                .Validate()
                .Build();

            Story story = null;
            StoryChapter firstChapter = null;
            //重试计数并统计累计消耗的时间与token
            var retryCount = 0;
            long totalTime = 0;
            var totalPromptTokens = 0;
            var totalCompletionTokens = 0;
            while (true)
            {
                try
                {
                    var result = await ExecuteFunctionCall<StoryChapter>(messages, chapterFunc);
                    totalTime += result.UseTime;
                    totalPromptTokens += result.PromptTokens;
                    totalCompletionTokens += result.CompletionTokens;
                    if (!result.Success)
                        throw new AIServiceException(result.Error);

                    firstChapter = result.ResultContent;

                    if (!CheckChatper(firstChapter, true))
                        throw new AIServiceException($"chapter content invalid:\n{result.Source}");

                    story = new Story()
                    {
                        Title = firstChapter.StoryTitle,
                        CreateTime = DateTime.UtcNow,
                        UpdateTime = DateTime.UtcNow,
                        IsPublic = true,
                        Model = _modelName,
                        StylePrompt = style.KeyWords,
                        Chapters = new List<StoryChapter>() { firstChapter },
                    };

                    firstChapter.Story = story;
                    firstChapter.CreateTime = DateTime.UtcNow;
                    firstChapter.UseTime = totalTime;
                    firstChapter.PromptTokens = totalPromptTokens;
                    firstChapter.CompletionTokens = totalCompletionTokens;
                    break;
                }
                catch (AIServiceException ex)
                {
                    retryCount++;
                    if (retryCount > _autoRetryMaxCount)
                        throw new AIServiceException($"error in generate new story of {style.Name}:\n {ex.Message}", ex);

                    _logger.LogWarning($"error in generate new story of {style.Name}, will retry {retryCount}:\n {ex.Message}");
                }
            }

            //生成分支剧情选项
            _ = await GenerateOptions(firstChapter);

            //生成分支剧情选项分数
            _ = await GenerateOptionsScore(firstChapter);

            style.UseTimes++;

            return story;
        }

        /// <summary>
        /// 生成故事的下一章节
        /// </summary>
        /// <returns></returns>
        public async Task<StoryChapter> GenerateNextChapter(StoryChapter previousChapter, int optionOrder = 0)
        {
            var story = previousChapter.Story;

            var messages = new List<ChatMessage> {
                ChatMessage.FromSystem("You are a writer, writing a long story. You'll write it chapter by chapter.\nYou're aware of the story's background, the content of the previous chapter, and the direction of the current chapter.\nBased on this, start writing the content for this chapter and summarize all the prior information."),
                ChatMessage.FromSystem($"The style of the story is:\n{story.StylePrompt}")
            };

            if (!string.IsNullOrEmpty(previousChapter.PreviousSummary))
            {
                messages.Add(ChatMessage.FromUser($"Background of the story:\n{previousChapter.PreviousSummary}"));
            }
            messages.Add(ChatMessage.FromUser($"Content of the previous chapter:\n{previousChapter.Content}"));

            var option = previousChapter.Options.Single(o => o.Order == optionOrder);
            messages.Add(ChatMessage.FromUser($"Requirements for the plot development of this chapter: {option.Name}，{option.Description}"));

            messages.Add(ChatMessage.FromSystem($"Include detailed dialogues, scene descriptions, and character actions. Split the content into multiple lines.\nQuantitative description of the plot: Impact scale is {option.ImpactScore} out of 5. Positivity is {option.PositivityScore} out of 5. Complexity is {option.ComplexityScore} out of 5.\nSuggested length is {_chapterLength} words."));
            messages.Add(ChatMessage.FromSystem($"You must respond in {_language}."));

            var chapterFunc = new FunctionDefinitionBuilder("chapter", "Write the next chapter and summarize the prior events.")
                .AddParameter("Title", PropertyDefinition.DefineString($"Title for this chapter, not exceeding {_titleMaxLength} words."))
                .AddParameter("Content", PropertyDefinition.DefineString($"Content for this chapter, suggested length is {_chapterLength} words."))
                .AddParameter("PreviousSummary", PropertyDefinition.DefineString($"Summarize the previous content for this chapter, suggested length is {_previousSummaryLength} words."))
                .Validate()
                .Build();

            StoryChapter chapter = null;
            //重试计数并统计累计消耗的时间与token
            var retryCount = 0;
            long totalTime = 0;
            var totalPromptTokens = 0;
            var totalCompletionTokens = 0;
            while (true)
            {
                try
                {
                    var result = await ExecuteFunctionCall<StoryChapter>(messages, chapterFunc);
                    totalTime += result.UseTime;
                    totalPromptTokens += result.PromptTokens;
                    totalCompletionTokens += result.CompletionTokens;
                    if (!result.Success)
                        throw new AIServiceException(result.Error);

                    chapter = result.ResultContent;
                    if (!CheckChatper(chapter))
                        throw new AIServiceException($"chapter content invalid:\n{result.Source}");

                    //为当前章节、前一章节和Story赋值
                    chapter.Story = story;
                    chapter.CreateTime = DateTime.UtcNow;
                    chapter.UseTime = totalTime;
                    chapter.PromptTokens = totalPromptTokens;
                    chapter.CompletionTokens = totalCompletionTokens;
                    chapter.PreviousOptionOrder = optionOrder;

                    break;
                }
                catch (AIServiceException ex)
                {
                    retryCount++;
                    if (retryCount > _autoRetryMaxCount)
                        throw new AIServiceException($"error in generate new chapter of {story.Title} after chapter {previousChapter.Title}:\n {ex.Message}", ex);

                    _logger.LogWarning($"error in generate new chapter of {story.Title} after chapter {previousChapter.Title}, will retry {retryCount}:\n {ex.Message}");
                }
            }

            //生成分支剧情选项
            _ = await GenerateOptions(chapter);

            //生成分支剧情选项分数
            _ = await GenerateOptionsScore(chapter);

            previousChapter.NextChapters ??= new List<StoryChapter>();
            previousChapter.NextChapters.Add(chapter);
            chapter.PreviousChapter = previousChapter;

            story.Chapters.Add(chapter);
            story.UpdateTime = DateTime.UtcNow;
            return chapter;
        }

        /// <summary>
        /// 检查章节内容是否符合标准
        /// </summary>
        /// <returns></returns>
        protected bool CheckChatper(StoryChapter chapter, bool isFirstChapter = false)
        {
            if (chapter == null
                || string.IsNullOrEmpty(chapter.Title)
                || !HasChineseChars(chapter.Title)
                || string.IsNullOrEmpty(chapter.Content)
                || !HasChineseChars(chapter.Content))
                return false;

            //第一章
            if (isFirstChapter)
            {
                if (string.IsNullOrEmpty(chapter.StoryTitle)
                    || !HasChineseChars(chapter.StoryTitle))
                    return false;
            }
            //非第一章
            else
            {
                if (string.IsNullOrEmpty(chapter.PreviousSummary)
                    || !HasChineseChars(chapter.PreviousSummary)
                    || chapter.PreviousSummary.Length < _previousSummaryLength / 4)
                    return false;
            }

            if (!HasChineseChars(chapter.Content)
                || chapter.Content.Length < _chapterLength / 2)//todo: 分析token进行内容长度检查，目前暂用字符串长度
                return false;

            return true;
        }

        /// <summary>
        /// 为章节生成后续选项
        /// </summary>
        /// <returns></returns>
        /// <returns>直接在原始章节上修改</returns>
        protected async Task<StoryChapter> GenerateOptions(StoryChapter chapter)
        {
            var messages = new List<ChatMessage> {
                ChatMessage.FromSystem($"You're a writer, working on a lengthy story. Design {_optionsCount} different options for next chapter of the story."),
                ChatMessage.FromSystem($"The style of the story is:\n{chapter.Story.StylePrompt}"),
            };

            if (chapter.PreviousChapter != null)
            {
                if (!string.IsNullOrEmpty(chapter.PreviousChapter.PreviousSummary))
                {
                    messages.Add(ChatMessage.FromUser($"Background of the story:\n{chapter.PreviousChapter.PreviousSummary}"));
                }
                messages.Add(ChatMessage.FromUser($"Content of the previous chapter:\n{chapter.PreviousChapter.Content}"));
            }
            messages.Add(ChatMessage.FromUser($"Content of this chapter:\n{chapter.Content}"));
            messages.Add(ChatMessage.FromUser($"Design {_optionsCount} options for the next chapter. Each option should have a distinct style. At least one should be simple and similar to the prior plot, and another should have significant changes and be more negative."));
            messages.Add(ChatMessage.FromSystem($"You must respond in  {_language} ."));

            var optionsFunc = new FunctionDefinitionBuilder("options", $"Generate {_optionsCount} options.")
                .AddParameter("Options", PropertyDefinition.DefineArray(
                    PropertyDefinition.DefineObject(
                        new Dictionary<string, PropertyDefinition>()
                        {
                            { "Order", PropertyDefinition.DefineInteger($"Options sequence number, starting from 1 to {_optionsCount}.") },
                            { "Name", PropertyDefinition.DefineString($"Options name, not exceeding {_optionNameMaxLength} words.") },
                            { "Description", PropertyDefinition.DefineString($"Detailed description for each option, not exceeding {_optionDescriptionMaxLength} words.") },
                        },
                        new List<string> { "Order", "Name", "Description" }, false, null, null)
                    ))
                .Validate()
                .Build();

            //重试计数并统计累计消耗的时间与token
            var retryCount = 0;
            long totalTime = 0;
            var totalPromptTokens = 0;
            var totalCompletionTokens = 0;
            while (true)
            {
                try
                {
                    var result = await ExecuteFunctionCall<StoryChapter>(messages, optionsFunc);
                    totalTime += result.UseTime;
                    totalPromptTokens += result.PromptTokens;
                    totalCompletionTokens += result.CompletionTokens;
                    if (!result.Success)
                        throw new AIServiceException(result.Error);

                    var resultOptions = result.ResultContent.Options;

                    if (!CheckOptions(resultOptions))
                        throw new AIServiceException($"options invalid:\n{result.Source}");

                    chapter.Options = resultOptions;
                    chapter.UseTime += totalTime;
                    chapter.PromptTokens += totalPromptTokens;
                    chapter.CompletionTokens += totalCompletionTokens;
                    return chapter;
                }
                catch (AIServiceException ex)
                {
                    retryCount++;
                    if (retryCount > _autoRetryMaxCount)
                        throw new AIServiceException($"error in generate options of {chapter.StoryTitle} - {chapter.Title}:\n {ex.Message}", ex);

                    _logger.LogWarning($"error in generate options of {chapter.StoryTitle} - {chapter.Title}, will retry {retryCount}:\n {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 检查选项是否符合标准
        /// </summary>
        /// <returns></returns>
        protected bool CheckOptions(IList<StoryChapterOption> options)
        {
            if (options == null || options.Count != _optionsCount)
                return false;

            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option.Order != i + 1
                    || string.IsNullOrEmpty(option.Name) || !HasChineseChars(option.Name)
                    || string.IsNullOrEmpty(option.Description) || !HasChineseChars(option.Description))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 为章节选项进行评分
        /// </summary>
        /// <returns>直接在原始章节上修改</returns>
        protected async Task<StoryChapter> GenerateOptionsScore(StoryChapter chapter)
        {
            if (chapter == null || chapter.Options == null || chapter.Options.Count != _optionsCount)
            {
                throw new ArgumentException("generate options score error: invalid arguments");
            }

            var messages = new List<ChatMessage> {
                ChatMessage.FromSystem($"You are a writer, working on a lengthy story. You now have {_optionsCount} option in your story. Rate each option in terms of its impact, positivity, and complexity."),
                ChatMessage.FromSystem($"The overall style of the story is as follows:\n{chapter.Story.StylePrompt}"),
                ChatMessage.FromUser($"Content of the latest chapter:\n{chapter.Content}"),
            };
            var optionsStr = string.Join('\n', chapter.Options.Select((option, index) => $"{index + 1}.{option.Name} {option.Description}"));

            messages.Add(ChatMessage.FromUser($"All {_optionsCount} options are:\n{optionsStr}"));
            messages.Add(ChatMessage.FromSystem($"You must respond in {_language}."));

            var optionsFunc = new FunctionDefinitionBuilder("optionsScore", "Rate each option.")
                .AddParameter("Options", PropertyDefinition.DefineArray(
                    PropertyDefinition.DefineObject(
                        new Dictionary<string, PropertyDefinition>()
                        {
                            { "Order", PropertyDefinition.DefineInteger($"Option sequence number, from 1 to {_optionsCount}.") },
                            { "PositivityScoreStr", PropertyDefinition.DefineEnum(new List<string>{"1","2","3","4","5"},"Positivity score for this option. Ranging from 1 to 5, with 1 being the most negative and 5 the most positive.") },
                            { "ImpactScoreStr", PropertyDefinition.DefineEnum(new List<string>{"1","2","3","4","5"},"Impact score for this plot option. Ranging from 1 to 5, with 1 being the least impactful and 5 the most impactful.") },
                            { "ComplexityScoreStr", PropertyDefinition.DefineEnum(new List<string>{"1","2","3","4","5"},"Complexity score for this plot option. Ranging from 1 to 5, with 1 being the simplest and 5 the most complex.") }
                        },
                        new List<string> { "Order", "ImpactScoreStr", "PositivityScoreStr", "ComplexityScoreStr" }, false, null, null)
                    ))
                .Validate()
                .Build();

            //重试计数并统计累计消耗的时间与token
            var retryCount = 0;
            long totalTime = 0;
            var totalPromptTokens = 0;
            var totalCompletionTokens = 0;
            while (true)
            {
                try
                {
                    var result = await ExecuteFunctionCall<StoryChapter>(messages, optionsFunc);
                    totalTime += result.UseTime;
                    totalPromptTokens += result.PromptTokens;
                    totalCompletionTokens += result.CompletionTokens;
                    if (!result.Success)
                        throw new AIServiceException(result.Error);

                    var resultOptions = result.ResultContent.Options;

                    if (resultOptions == null || resultOptions.Count != _optionsCount)
                    {
                        throw new AIServiceException($"invalid options count: {resultOptions?.Count}");
                    }

                    //将分数赋给原始章节选项，逐个解析分数，有问题则抛出异常
                    foreach (var option in chapter.Options)
                    {
                        var resultOption = resultOptions.SingleOrDefault(o => o.Order == option.Order);
                        if (resultOption == null)
                        {
                            throw new AIServiceException($"cannot find option order {option.Order}");
                        }

                        if (!int.TryParse(resultOption.PositivityScoreStr, out int positivity)
                            || !int.TryParse(resultOption.ImpactScoreStr, out int impart)
                            || !int.TryParse(resultOption.ComplexityScoreStr, out int complexity))
                        {
                            throw new AIServiceException($"wrong score: \n{JsonSerializer.Serialize(resultOption)}");
                        }

                        option.PositivityScore = positivity;
                        option.ImpactScore = impart;
                        option.ComplexityScore = complexity;
                    }

                    chapter.UseTime += result.UseTime;
                    chapter.PromptTokens += result.PromptTokens;
                    chapter.CompletionTokens += result.CompletionTokens;
                    return chapter;
                }
                catch (AIServiceException ex)
                {
                    retryCount++;
                    if (retryCount > _autoRetryMaxCount)
                        throw new AIServiceException($"error in generate options score of {chapter.StoryTitle} - {chapter.Title}:\n {ex.Message}", ex);

                    _logger.LogWarning($"error in generate options score of {chapter.StoryTitle} - {chapter.Title}, will retry {retryCount}:\n {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 调用OpenAI执行函数计算并返回结果
        /// </summary>
        /// <returns></returns>
        protected async Task<FunctionCallResult<T>> ExecuteFunctionCall<T>(IList<ChatMessage> messages, FunctionDefinition function)
        {
            _logger.LogDebug($"try start function call {function.Name}");
            var sw = new Stopwatch();
            sw.Start();
            var completionResult = await _sdk.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = messages,
                Functions = new List<FunctionDefinition> { function },
                FunctionCall = new Dictionary<string, string> { { "name", function.Name } }
            });
            var useTime = sw.ElapsedMilliseconds;

            var result = new FunctionCallResult<T>()
            {
                UseTime = useTime,
                PromptTokens = completionResult.Usage.PromptTokens,
                CompletionTokens = completionResult.Usage.CompletionTokens ?? 0,
            };

            if (!completionResult.Successful)
            {
                if (completionResult.Error == null)
                {
                    result.Error = $"call chatgpt unknown error";
                    return result;
                }
                result.Error = $"call chatgpt error: {completionResult.Error.Message}";
                return result;
            }
            if (completionResult.Choices.First().Message.FunctionCall == null)
            {
                result.Error = $"call chatgpt error: no function call result";
                return result;
            }

            try
            {
                var jsonResult = EscapeSpecialChars(completionResult.Choices.First().Message.FunctionCall.Arguments);
                result.Source = jsonResult;
                result.ResultContent = JsonSerializer.Deserialize<T>(jsonResult, _defaultJsonSerializerOptions);

                _logger.LogDebug($"complete function call {function.Name} in {useTime}ms, {result.PromptTokens}+{result.CompletionTokens}={result.TotalTokens} tokens.");

                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"deserialize chatgpt return json error:\n{ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 处理json属性中的换行符以避免转义问题
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        protected string EscapeSpecialChars(string input)
        {
            string pattern = @"("":\s*"")[^""\\]*(\\.[^""\\]*)*""";
            return Regex.Replace(input, pattern, m => Regex.Replace(m.Value, @"(\n)+", "\\n"));
        }

        /// <summary>
        /// 检查字符串是否包含中文字符
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private bool HasChineseChars(string input)
        {
            foreach (char c in input)
            {
                if (c >= 0x4e00 && c <= 0x9fa5)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 返回函数计算调用结果实体类
        /// </summary>
        protected class FunctionCallResult<T>
        {
            public bool Success { get { return string.IsNullOrEmpty(Error); } }
            public string? Error { get; set; }
            public string Source { get; set; }
            public T ResultContent { get; set; }
            public int PromptTokens { get; set; }
            public int CompletionTokens { get; set; }
            public int TotalTokens { get { return PromptTokens + CompletionTokens; } }
            public long UseTime { get; set; }
        }

        /// <summary>
        /// AI服务相关异常类
        /// </summary>
        [Serializable]
        public class AIServiceException : Exception
        {
            public AIServiceException() { }
            public AIServiceException(string message) : base(message) { }
            public AIServiceException(string message, Exception inner) : base(message, inner) { }
            protected AIServiceException(System.Runtime.Serialization.SerializationInfo info,
                System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        }
    }
}
