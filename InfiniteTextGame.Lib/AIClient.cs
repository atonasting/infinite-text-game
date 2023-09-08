using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections;
using OpenAI.Builders;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.SharedModels;
using System.Text.Json;
using InfiniteTextGame.Models;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;
using InfiniteTextGame.Lib.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using OpenAI.ObjectModels.ResponseModels;
using Microsoft.Extensions.Logging;

namespace InfiniteTextGame.Lib
{
    public class AIClient
    {
        private ILogger _logger;
        private OpenAIService _sdk;
        private ProviderType _providerType;
        private string _modelName;//用于记录的模型名
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
        public AIClient(string apiKey, string? defaultModel, string? proxy, ILogger<AIClient> logger)
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
        public AIClient(string apiKey, string resourceName, string deploymentId, string? proxy, ILogger<AIClient> logger)
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

            var writingStyleFunc = new FunctionDefinitionBuilder("WritingStyle")
                .AddParameter("Name", PropertyDefinition.DefineString("用10个以内的汉字总结文字风格"))
                .AddParameter("KeyWordList", PropertyDefinition.DefineArray(
                    PropertyDefinition.DefineString("从作品片段中总结出的文字风格关键字。关键字可以是名词、形容词+名词、动词+名词的格式；每个关键字不多于20个汉字；不要包含标点符号；不涉及剧情中的人物")))
                .Validate()
                .Build();

            //计算token数量
            var fullSourceText = string.Join("\n\n", Source);
            var tokenCount = OpenAI.Tokenizer.GPT3.TokenizerGpt3.TokenCount(fullSourceText, true);

            var completionResult = await _sdk.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = messages,
                Functions = new List<FunctionDefinition> { writingStyleFunc },
                FunctionCall = new Dictionary<string, string> { { "name", "WritingStyle" } }
            });

            if (!completionResult.Successful)
            {
                if (completionResult.Error == null)
                {
                    throw new ApplicationException($"Call chatgpt unknown error");
                }
                throw new ApplicationException($"Call chatgpt error: {completionResult.Error.Message}");
            }
            if (completionResult.Choices.First().Message.FunctionCall == null)
            {
                throw new ApplicationException($"Call chatgpt function call error");
            }

            WritingStyle style;
            try
            {
                style = JsonSerializer.Deserialize<WritingStyle>(
                        completionResult.Choices.First().Message.FunctionCall.Arguments, _defaultJsonSerializerOptions);
                style.KeyWords = string.Join(',', style.KeyWordList);
                return style;
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"deserialize chatgpt return json error:\n{ex.Message}\n{completionResult.Choices.First().Message.FunctionCall.Arguments}");
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

            var sw = new Stopwatch();
            sw.Start();
            var completionResult = await _sdk.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = messages,
                Functions = new List<FunctionDefinition> { chapterFunc },
                FunctionCall = new Dictionary<string, string> { { "name", "chapter" } }
            });
            var useTime = sw.ElapsedMilliseconds;

            //处理返回结果
            if (!completionResult.Successful)
            {
                if (completionResult.Error == null)
                {
                    throw new ApplicationException($"Call chatgpt unknown error");
                }
                throw new ApplicationException($"Call chatgpt error: {completionResult.Error.Message}");
            }
            if (completionResult.Choices.First().Message.FunctionCall == null)
            {
                throw new ApplicationException($"Call chatgpt function call error");
            }

            StoryChapter firstChapter;
            try
            {
                var jsonResult = completionResult.Choices.First().Message.FunctionCall.Arguments;
                firstChapter = JsonSerializer.Deserialize<StoryChapter>(jsonResult, _defaultJsonSerializerOptions);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"deserialize chatgpt return json error:\n{ex.Message}\n{completionResult.Choices.First().Message.FunctionCall.Arguments}");
            }

            var story = new Story()
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
            firstChapter.UseTime = useTime;
            firstChapter.PromptTokens = completionResult.Usage.PromptTokens;
            firstChapter.CompletionTokens = (int)completionResult.Usage.CompletionTokens;

            //生成分支剧情选项
            var optionsChapter = await GenerateOptions(firstChapter);//此章节对象仅用于记录分支和运行情况，不影响原始章节内容
            firstChapter.UseTime += optionsChapter.UseTime;
            firstChapter.PromptTokens += optionsChapter.PromptTokens;
            firstChapter.CompletionTokens += optionsChapter.CompletionTokens;
            firstChapter.Options = optionsChapter.Options;

            //生成分支剧情选项分数
            var optionsChapterWithScore = await GenerateOptionsScore(firstChapter);
            firstChapter.UseTime += optionsChapterWithScore.UseTime;
            firstChapter.PromptTokens += optionsChapterWithScore.PromptTokens;
            firstChapter.CompletionTokens += optionsChapterWithScore.CompletionTokens;
            firstChapter.Options = optionsChapterWithScore.Options;

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

            var sw = new Stopwatch();
            sw.Start();
            var completionResult = await _sdk.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = messages,
                Functions = new List<FunctionDefinition> { chapterFunc },
                FunctionCall = new Dictionary<string, string> { { "name", "chapter" } }
            });
            var useTime = sw.ElapsedMilliseconds;

            if (!completionResult.Successful)
            {
                if (completionResult.Error == null)
                {
                    throw new ApplicationException($"Call chatgpt unknown error");
                }
                throw new ApplicationException($"Call chatgpt error: {completionResult.Error.Message}");
            }
            if (completionResult.Choices.First().Message.FunctionCall == null)
            {
                throw new ApplicationException($"Call chatgpt function call error");
            }

            StoryChapter chapter;
            try
            {
                var jsonResult = completionResult.Choices.First().Message.FunctionCall.Arguments;
                chapter = JsonSerializer.Deserialize<StoryChapter>(jsonResult, _defaultJsonSerializerOptions);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"deserialize chatgpt return json error:\n{ex.Message}\n{completionResult.Choices.First().Message.FunctionCall.Arguments}");
            }

            //为当前章节、前一章节和Story赋值
            chapter.Story = story;
            chapter.CreateTime = DateTime.UtcNow;
            chapter.UseTime = useTime;
            chapter.PromptTokens = completionResult.Usage.PromptTokens;
            chapter.CompletionTokens = (int)completionResult.Usage.CompletionTokens;
            chapter.PreviousOptionOrder = optionOrder;

            //生成分支剧情选项
            var optionsChapter = await GenerateOptions(chapter);//此章节对象仅用于记录分支和运行情况，不影响原始章节内容
            chapter.UseTime += optionsChapter.UseTime;
            chapter.PromptTokens += optionsChapter.PromptTokens;
            chapter.CompletionTokens += optionsChapter.CompletionTokens;
            chapter.Options = optionsChapter.Options;

            //生成分支剧情选项分数
            var optionsChapterWithScore = await GenerateOptionsScore(chapter);
            chapter.UseTime += optionsChapterWithScore.UseTime;
            chapter.PromptTokens += optionsChapterWithScore.PromptTokens;
            chapter.CompletionTokens += optionsChapterWithScore.CompletionTokens;
            chapter.Options = optionsChapterWithScore.Options;

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
        protected async Task<StoryChapter> GenerateOptions(StoryChapter chapter)
        {
            var messages = new List<ChatMessage> {
                ChatMessage.FromSystem("你是一位作家，你正在编写一部长篇故事。你要为故事的最新章节设计后续剧情的4个分支剧情，每个分支剧情要具有不同风格。"),
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
            messages.Add(ChatMessage.FromUser($"你要为后续剧情设计4个分支剧情。建议各个分支剧情的风格要各自不同，既有影响规模较小的，也有影响规模较大的；既有正面的，也有负面的；既有简单的，也有复杂的。"));

            var optionsFunc = new FunctionDefinitionBuilder("options", "生成后续4个分支剧情")
                .AddParameter("Options", PropertyDefinition.DefineArray(
                    PropertyDefinition.DefineObject(
                        new Dictionary<string, PropertyDefinition>()
                        {
                            { "Order", PropertyDefinition.DefineInteger("分支剧情的序号，按顺序分别为1、2、3、4") },
                            { "Name", PropertyDefinition.DefineString("分支剧情名称，长度不超过4个单词") },
                            { "Description", PropertyDefinition.DefineString("每条分支剧情的详细解释，长度不超过8个单词") },
                        },
                        new List<string> { "Order", "Name", "Description" }, false, null, null)
                    ))
                .Validate()
                .Build();

            var sw = new Stopwatch();
            sw.Start();
            var completionResult = await _sdk.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = messages,
                Functions = new List<FunctionDefinition> { optionsFunc },
                FunctionCall = new Dictionary<string, string> { { "name", "options" } }
            });
            var useTime = sw.ElapsedMilliseconds;

            if (!completionResult.Successful)
            {
                if (completionResult.Error == null)
                {
                    throw new ApplicationException($"Call chatgpt unknown error");
                }
                throw new ApplicationException($"Call chatgpt error: {completionResult.Error.Message}");
            }
            if (completionResult.Choices.First().Message.FunctionCall == null)
            {
                throw new ApplicationException($"Call chatgpt function call error");
            }

            StoryChapter resultChapter;
            try
            {
                var jsonResult = completionResult.Choices.First().Message.FunctionCall.Arguments;
                resultChapter = JsonSerializer.Deserialize<StoryChapter>(jsonResult, _defaultJsonSerializerOptions);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"deserialize chatgpt return json error:\n{ex.Message}\n{completionResult.Choices.First().Message.FunctionCall.Arguments}");
            }

            if (resultChapter.Options == null || resultChapter.Options.Count != 4)
            {
                throw new ApplicationException($"generate options error");
            }

            resultChapter.UseTime = useTime;
            resultChapter.PromptTokens = completionResult.Usage.PromptTokens;
            resultChapter.CompletionTokens = (int)completionResult.Usage.CompletionTokens;
            return resultChapter;
        }

        /// <summary>
        /// 为选项进行评分
        /// </summary>
        /// <returns></returns>
        protected async Task<StoryChapter> GenerateOptionsScore(StoryChapter chapter)
        {
            var messages = new List<ChatMessage> {
                ChatMessage.FromSystem("你是一位作家，你正在编写一部长篇故事。现在你的故事出现了4个分支剧情，你要从影响规模、正面程度和复杂程度三个角度来为每个分支剧情评分。"),
                ChatMessage.FromSystem($"整部故事的风格如下：\n{chapter.Story.StylePrompt}"),
                ChatMessage.FromUser($"最新一章的故事内容:\n{chapter.Content}"),
        };
            var optionsStr = string.Join('\n', chapter.Options.Select((option, index) => $"{index + 1}.{option.Name} {option.Description}"));

            messages.Add(ChatMessage.FromUser($"它的4个分支剧情是：\n{optionsStr}"));

            var optionsFunc = new FunctionDefinitionBuilder("optionsScore", "为每个分支剧情打分")
                .AddParameter("Options", PropertyDefinition.DefineArray(
                    PropertyDefinition.DefineObject(
                        new Dictionary<string, PropertyDefinition>()
                        {
                            { "Order", PropertyDefinition.DefineInteger("分支剧情的序号，按顺序分别为1、2、3、4") },
                            { "PositivityScoreStr", PropertyDefinition.DefineEnum(new List<string>{"1","2","3","4","5"},"这段分支剧情的正面程度分数。取值范围在1~5之间，最负面为1，最正面为5") },
                            { "ImpactScoreStr", PropertyDefinition.DefineEnum(new List<string>{"1","2","3","4","5"},"这段分支剧情的影响规模分数。取值范围在1~5之间，规模最小为1，最大为5") },
                            { "ComplexityScoreStr", PropertyDefinition.DefineEnum(new List<string>{"1","2","3","4","5"},"这段分支剧情的复杂程度分数。取值范围在1~5之间，最简单为1，最复杂为5") }
                        },
                        new List<string> { "Order", "ImpactScoreStr", "PositivityScoreStr", "ComplexityScoreStr" }, false, null, null)
                    ))
                .Validate()
                .Build();

            var sw = new Stopwatch();
            sw.Start();
            var completionResult = await _sdk.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = messages,
                Functions = new List<FunctionDefinition> { optionsFunc },
                FunctionCall = new Dictionary<string, string> { { "name", "optionsScore" } }
            });
            var useTime = sw.ElapsedMilliseconds;

            if (!completionResult.Successful)
            {
                if (completionResult.Error == null)
                {
                    throw new ApplicationException($"Call chatgpt unknown error");
                }
                throw new ApplicationException($"Call chatgpt error: {completionResult.Error.Message}");
            }
            if (completionResult.Choices.First().Message.FunctionCall == null)
            {
                throw new ApplicationException($"Call chatgpt function call error");
            }

            StoryChapter resultChapter;
            try
            {
                var jsonResult = completionResult.Choices.First().Message.FunctionCall.Arguments;
                resultChapter = JsonSerializer.Deserialize<StoryChapter>(jsonResult, _defaultJsonSerializerOptions);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"deserialize chatgpt return json error:\n{ex.Message}\n{completionResult.Choices.First().Message.FunctionCall.Arguments}");
            }

            if (resultChapter.Options == null || resultChapter.Options.Count != 4)
            {
                throw new ApplicationException($"generate options score error");
            }

            //将分数赋给原始章节选项，逐个解析分数，有问题则抛出异常
            foreach (var option in chapter.Options)
            {
                var resultOption = resultChapter.Options.SingleOrDefault(o => o.Order == option.Order);
                if (resultOption == null)
                {
                    throw new ApplicationException($"generate options score error: cannot find option order {option.Order}");
                }

                if (!int.TryParse(resultOption.PositivityScoreStr, out int positivity)
                    || !int.TryParse(resultOption.ImpactScoreStr, out int impart)
                    || !int.TryParse(resultOption.ComplexityScoreStr, out int complexity))
                {
                    throw new ApplicationException($"generate options score error: \n{JsonSerializer.Serialize(resultOption)}");
                }

                option.PositivityScore = positivity;
                option.ImpactScore = impart;
                option.ComplexityScore = complexity;
            }

            resultChapter.Options = chapter.Options;

            resultChapter.UseTime = useTime;
            resultChapter.PromptTokens = completionResult.Usage.PromptTokens;
            resultChapter.CompletionTokens = (int)completionResult.Usage.CompletionTokens;
            return resultChapter;
        }
    }
}
