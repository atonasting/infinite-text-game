using OpenAI.Managers;
using OpenAI;
using OpenAI.Builders;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.SharedModels;
using System.Text.Json;
using InfiniteTextGame.Models;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

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

        private const int _optionsCount = 4;//选项数量
        private readonly int _chapterLength = 400;//默认每段单词数（暂定）
        private readonly int _previousSummaryLength = 200;//默认前情提要单词数（暂定）

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
        /// 基于原始文章总结关键词
        /// </summary>
        public async Task<WritingStyle> GenerateWritingStyle(string Source)
        {
            var messages = new List<ChatMessage> {
                ChatMessage.FromSystem("你是一位经验丰富的文学编辑。你会阅读大段的文学作品并用精炼的语言准确总结出这些作品的特征，将这些特征整理成5到15个关键词，每个关键词不多于20个汉字。这些关键词被提供给其他人工智能模型，能够尽可能准确地还原出作者的文字风格。接下来会为你提供一些作品片段。"),
                ChatMessage.FromUser(Source)
            };

            var writingStyleFunc = new FunctionDefinitionBuilder("writingStyle")
                .AddParameter("Name", PropertyDefinition.DefineString("用10个以内的汉字总结文字风格"))
                .AddParameter("KeyWordList", PropertyDefinition.DefineArray(
                    PropertyDefinition.DefineString("从作品片段中总结出的文字风格关键字。关键字可以是名词、形容词+名词、动词+名词的格式；每个关键字不多于20个汉字；不要包含标点符号；不涉及剧情中的人物")))
                .Validate()
                .Build();

            //计算token数量以区别使用模型（暂不使用）
            var fullSourceText = string.Join("\n\n", Source);
            var tokenCount = OpenAI.Tokenizer.GPT3.TokenizerGpt3.TokenCount(fullSourceText, true);

            try
            {
                var result = await ExecuteFunctionCall<WritingStyle>(messages, writingStyleFunc);
                var style = result.ResultContent;
                style.KeyWords = string.Join(',', style.KeyWordList);
                return style;
            }
            catch (AIServiceException ex)
            {
                throw new AIServiceException($"error in generate writing style: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 生成故事以及第一章
        /// </summary>
        /// <returns></returns>
        public async Task<Story> GenerateStory(WritingStyle Style)
        {
            var messages = new List<ChatMessage> {
                ChatMessage.FromSystem($"你是一位作家，你正在编写一部长篇故事。\n整部故事的风格如下：{Style.KeyWords}"),
                ChatMessage.FromUser($"首先编写故事的第一个章节，需要交待故事的背景和主要人物，文字描写要细致。建议长度为{_chapterLength}个单词"),
            };

            var chapterFunc = new FunctionDefinitionBuilder("chapter", "章节内容")
                .AddParameter("StoryTitle", PropertyDefinition.DefineString($"故事标题，长度不超过4个单词"))
                .AddParameter("Title", PropertyDefinition.DefineString($"本章节标题，长度不超过4个单词。不要包含“第一章”以及类似的编号"))
                .AddParameter("Content", PropertyDefinition.DefineString($"本章节内容，建议长度为{_chapterLength}个单词"))
                .Validate()
                .Build();

            Story story = null;
            StoryChapter firstChapter = null;
            try
            {
                var result = await ExecuteFunctionCall<StoryChapter>(messages, chapterFunc);
                firstChapter = result.ResultContent;

                story = new Story()
                {
                    Title = firstChapter.StoryTitle,
                    CreateTime = DateTime.UtcNow,
                    UpdateTime = DateTime.UtcNow,
                    IsPublic = true,
                    Model = _modelName,
                    StylePrompt = Style.KeyWords,
                    Chapters = new List<StoryChapter>() { firstChapter },
                };

                firstChapter.Story = story;
                firstChapter.CreateTime = DateTime.UtcNow;
                firstChapter.UseTime = result.UseTime;
                firstChapter.PromptTokens = result.PromptTokens;
                firstChapter.CompletionTokens = result.CompletionTokens;
            }
            catch (AIServiceException ex)
            {
                throw new AIServiceException($"error in generate new story of {_modelName}: {ex.Message}", ex);
            }

            //生成分支剧情选项
            _ = await GenerateOptions(firstChapter);

            //生成分支剧情选项分数
            _ = await GenerateOptionsScore(firstChapter);

            Style.UseTimes++;

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
                ChatMessage.FromSystem("你是一位作家，你正在编写一部长篇故事。编写是逐个章节进行的。\n你能掌握之前的故事背景、上个章节的内容、以及当前章节的发展方向。\n在这些内容基础上开始编写本章节内容，并总结本章节之前的所有前情提要。"),
                ChatMessage.FromSystem($"整部故事的风格如下：\n{story.StylePrompt}")
            };

            if (!string.IsNullOrEmpty(previousChapter.PreviousSummary))
            {
                messages.Add(ChatMessage.FromUser($"故事的前情提要:\n{previousChapter.PreviousSummary}"));
            }
            messages.Add(ChatMessage.FromUser($"前一章的故事内容:\n{previousChapter.Content}"));

            var option = previousChapter.Options.Single(o => o.Order == optionOrder);
            messages.Add(ChatMessage.FromUser($"对本章节剧情发展方向的要求：{option.Name}，{option.Description}。\n其中影响规模为{option.ImpactScore}分（规模最小为1分，最大为5分）\n正面程度为{option.PositivityScore}分（最负面为1分，最正面为5分）\n复杂程度为{option.ComplexityScore}分（最简单为1分，最复杂为5分）\n章节建议长度为{_chapterLength}个单词。"));

            var chapterFunc = new FunctionDefinitionBuilder("chapter", "编写下一章节内容并总结前情提要")
                .AddParameter("Title", PropertyDefinition.DefineString($"本章节标题，长度不超过4个单词"))
                .AddParameter("Content", PropertyDefinition.DefineString($"本章节内容，建议长度为{_chapterLength}个单词"))
                .AddParameter("PreviousSummary", PropertyDefinition.DefineString($"根据之前所有内容总结出本章节的前情提要，建议长度为{_previousSummaryLength}个单词"))
                .Validate()
                .Build();

            StoryChapter chapter = null;
            try
            {
                var result = await ExecuteFunctionCall<StoryChapter>(messages, chapterFunc);
                chapter = result.ResultContent;

                //为当前章节、前一章节和Story赋值
                chapter.Story = story;
                chapter.CreateTime = DateTime.UtcNow;
                chapter.UseTime = result.UseTime;
                chapter.PromptTokens = result.PromptTokens;
                chapter.CompletionTokens = result.CompletionTokens;
                chapter.PreviousOptionOrder = optionOrder;
            }
            catch (AIServiceException ex)
            {
                throw new AIServiceException($"error in generate new chapter of {story.Title} after chapter {previousChapter.Title} : {ex.Message}", ex);
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
        /// 为章节生成后续选项
        /// </summary>
        /// <returns></returns>
        /// <returns>直接在原始章节上修改</returns>
        protected async Task<StoryChapter> GenerateOptions(StoryChapter chapter)
        {
            var messages = new List<ChatMessage> {
                ChatMessage.FromSystem($"你是一位作家，你正在编写一部长篇故事。你要为故事的最新章节设计后续剧情的{_optionsCount}个分支剧情，每个分支剧情要具有不同风格。"),
                ChatMessage.FromSystem($"整部故事的风格如下：\n{chapter.Story.StylePrompt}"),
            };

            if (chapter.PreviousChapter != null)
            {
                if (!string.IsNullOrEmpty(chapter.PreviousChapter.PreviousSummary))
                {
                    messages.Add(ChatMessage.FromUser($"故事的前情提要:\n{chapter.PreviousChapter.PreviousSummary}"));
                }
                messages.Add(ChatMessage.FromUser($"前一章的故事内容:\n{chapter.PreviousChapter.Content}"));
            }
            messages.Add(ChatMessage.FromUser($"最新一章的故事内容:\n{chapter.Content}"));
            messages.Add(ChatMessage.FromUser($"你要为后续剧情设计{_optionsCount}个分支剧情。建议各个分支剧情的风格要各自不同，既有影响规模较小的，也有影响规模较大的；既有正面的，也有负面的；既有简单的，也有复杂的。"));

            var optionsFunc = new FunctionDefinitionBuilder("options", $"生成后续{_optionsCount}个分支剧情")
                .AddParameter("Options", PropertyDefinition.DefineArray(
                    PropertyDefinition.DefineObject(
                        new Dictionary<string, PropertyDefinition>()
                        {
                            { "Order", PropertyDefinition.DefineInteger($"分支剧情的序号，从1开始递增到{_optionsCount}") },
                            { "Name", PropertyDefinition.DefineString("分支剧情名称，长度不超过4个单词") },
                            { "Description", PropertyDefinition.DefineString("每条分支剧情的详细解释，长度不超过8个单词") },
                        },
                        new List<string> { "Order", "Name", "Description" }, false, null, null)
                    ))
                .Validate()
                .Build();

            try
            {
                var result = await ExecuteFunctionCall<StoryChapter>(messages, optionsFunc);
                var resultChapter = result.ResultContent;

                if (resultChapter.Options == null || resultChapter.Options.Count != _optionsCount)
                {
                    throw new AIServiceException($"invalid options count: {resultChapter.Options?.Count}");
                }

                chapter.Options = resultChapter.Options;
                chapter.UseTime += result.UseTime;
                chapter.PromptTokens += result.PromptTokens;
                chapter.CompletionTokens += result.CompletionTokens;
                return chapter;
            }
            catch (AIServiceException ex)
            {
                throw new AIServiceException($"error in generate options of {chapter.StoryTitle} - {chapter.Title}: {ex.Message}", ex);
            }
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
                ChatMessage.FromSystem($"你是一位作家，你正在编写一部长篇故事。现在你的故事出现了{_optionsCount}个分支剧情，你要从影响规模、正面程度和复杂程度三个角度来为每个分支剧情评分。"),
                ChatMessage.FromSystem($"整部故事的风格如下：\n{chapter.Story.StylePrompt}"),
                ChatMessage.FromUser($"最新一章的故事内容:\n{chapter.Content}"),
            };
            var optionsStr = string.Join('\n', chapter.Options.Select((option, index) => $"{index + 1}.{option.Name} {option.Description}"));

            messages.Add(ChatMessage.FromUser($"它的{_optionsCount}个分支剧情是：\n{optionsStr}"));

            var optionsFunc = new FunctionDefinitionBuilder("optionsScore", "为每个分支剧情打分")
                .AddParameter("Options", PropertyDefinition.DefineArray(
                    PropertyDefinition.DefineObject(
                        new Dictionary<string, PropertyDefinition>()
                        {
                            { "Order", PropertyDefinition.DefineInteger($"分支剧情的序号，从1开始递增到{_optionsCount}") },
                            { "PositivityScoreStr", PropertyDefinition.DefineEnum(new List<string>{"1","2","3","4","5"},"这段分支剧情的正面程度分数。取值范围在1~5之间，最负面为1，最正面为5") },
                            { "ImpactScoreStr", PropertyDefinition.DefineEnum(new List<string>{"1","2","3","4","5"},"这段分支剧情的影响规模分数。取值范围在1~5之间，规模最小为1，最大为5") },
                            { "ComplexityScoreStr", PropertyDefinition.DefineEnum(new List<string>{"1","2","3","4","5"},"这段分支剧情的复杂程度分数。取值范围在1~5之间，最简单为1，最复杂为5") }
                        },
                        new List<string> { "Order", "ImpactScoreStr", "PositivityScoreStr", "ComplexityScoreStr" }, false, null, null)
                    ))
                .Validate()
                .Build();

            try
            {
                var result = await ExecuteFunctionCall<StoryChapter>(messages, optionsFunc);
                var resultChapter = result.ResultContent;

                if (resultChapter.Options == null || resultChapter.Options.Count != _optionsCount)
                {
                    throw new AIServiceException($"invalid options count: {resultChapter.Options?.Count}");
                }

                //将分数赋给原始章节选项，逐个解析分数，有问题则抛出异常
                foreach (var option in chapter.Options)
                {
                    var resultOption = resultChapter.Options.SingleOrDefault(o => o.Order == option.Order);
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
                throw new AIServiceException($"error in generate options score of {chapter.StoryTitle} - {chapter.Title}: {ex.Message}", ex);
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

            if (!completionResult.Successful)
            {
                if (completionResult.Error == null)
                {
                    throw new AIServiceException($"call chatgpt unknown error");
                }
                throw new AIServiceException($"call chatgpt error: {completionResult.Error.Message}");
            }
            if (completionResult.Choices.First().Message.FunctionCall == null)
            {
                throw new AIServiceException($"call chatgpt error: no function call result");
            }

            try
            {
                var jsonResult = completionResult.Choices.First().Message.FunctionCall.Arguments;
                T resultContent = JsonSerializer.Deserialize<T>(jsonResult, _defaultJsonSerializerOptions);

                var result = new FunctionCallResult<T>()
                {
                    ResultContent = resultContent,
                    UseTime = useTime,
                    PromptTokens = completionResult.Usage.PromptTokens,
                    CompletionTokens = completionResult.Usage.CompletionTokens ?? 0,
                };

                _logger.LogDebug($"complete function call {function.Name} in {useTime}ms, {result.PromptTokens}+{result.CompletionTokens}={result.TotalTokens} tokens.");

                return result;
            }
            catch (Exception ex)
            {
                throw new AIServiceException($"deserialize chatgpt return json error:\n{ex.Message}\n{completionResult.Choices.First().Message.FunctionCall.Arguments}");
            }
        }

        /// <summary>
        /// 返回函数计算调用结果实体类
        /// </summary>
        protected class FunctionCallResult<T>
        {
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
