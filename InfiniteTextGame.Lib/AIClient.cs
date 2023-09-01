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

namespace InfiniteTextGame.Lib
{
    public class AIClient
    {
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
        public AIClient(string apiKey, string? defaultModel, string? proxy)
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
        }

        /// <summary>
        /// Azure构造函数
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="proxy"></param>
        public AIClient(string apiKey, string resourceName, string deploymentId, string? proxy)
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
                ChatMessage.FromSystem($"你是一位作家，你正在编写一部长篇故事。\n整部故事的文字风格有如下特征：{Style.KeyWords}"),
                ChatMessage.FromUser($"首先编写故事的第一个章节，需要交待故事的背景和主要人物，文字描写要细致。建议长度为{_chapterLength}个单词"),
            };

            var chapterFunc = new FunctionDefinitionBuilder("chapter", "章节内容")
                .AddParameter("StoryTitle", PropertyDefinition.DefineString($"故事标题，长度不超过4个单词"))
                .AddParameter("Title", PropertyDefinition.DefineString($"本章节标题，长度不超过4个单词"))
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
                var jsonResult = completionResult.Choices.First().Message.FunctionCall.Arguments
                    .Replace((char)0x0D, ' ')
                    .Replace((char)0x0A, '\n');
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
            var optionsChapter = await GenerateOptions(firstChapter);//此章节对象仅用于记录分支和运行情况
            firstChapter.UseTime += optionsChapter.UseTime;
            firstChapter.PromptTokens += optionsChapter.PromptTokens;
            firstChapter.CompletionTokens += optionsChapter.CompletionTokens;
            firstChapter.Options = optionsChapter.Options;

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
                ChatMessage.FromSystem($"整部故事的文字风格有如下特征：\n{story.StylePrompt}")
            };

            if (!string.IsNullOrEmpty(previousChapter.PreviousSummary))
            {
                messages.Add(ChatMessage.FromUser($"故事的前情提要:\n{previousChapter.PreviousSummary}"));
            }
            messages.Add(ChatMessage.FromUser($"前一章的故事内容:\n{previousChapter.Content}"));

            var option = previousChapter.Options.Single(o => o.Order == optionOrder);
            messages.Add(ChatMessage.FromUser($"对本章节剧情的要求：{option.Name}，{option.Description}。\n章节建议长度为{_chapterLength}个单词。"));
            messages.Add(ChatMessage.FromSystem($"你要通过动作、对话、描写等方式来编写故事，尽可能避免单纯地写剧情本身。"));

            var chapterFunc = new FunctionDefinitionBuilder("chapter", "生成下一章节内容并总结前情提要")
                .AddParameter("Title", PropertyDefinition.DefineString($"本章节标题，长度不超过4个单词"))
                .AddParameter("Content", PropertyDefinition.DefineString($"本章节内容，建议长度为{_chapterLength}个单词"))
                .AddParameter("PreviousSummary", PropertyDefinition.DefineString($"从前情提要和上一章节的内容总结出本章节的前情提要，建议长度为{_previousSummaryLength}个单词"))
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
                //修复一些换行符解析问题
                var jsonResult = completionResult.Choices.First().Message.FunctionCall.Arguments
                    .Replace((char)0x0D, ' ')
                    .Replace((char)0x0A, '\n');
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
            var optionsChapter = await GenerateOptions(chapter);//此章节对象仅用于记录分支和运行情况
            chapter.UseTime += optionsChapter.UseTime;
            chapter.PromptTokens += optionsChapter.PromptTokens;
            chapter.CompletionTokens += optionsChapter.CompletionTokens;
            chapter.Options = optionsChapter.Options;

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
                ChatMessage.FromSystem("你是一位作家，你正在编写一部长篇故事。你要为故事的最新章节设计后续剧情的4个分支剧情，每个剧情要具有不同风格。"),
                ChatMessage.FromSystem($"整部故事的文字风格有如下特征：\n{chapter.Story.StylePrompt}"),
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
            messages.Add(ChatMessage.FromUser($"你要为后续剧情设计4个分支剧情，每个剧情要具有不同风格。"));

            var optionsFunc = new FunctionDefinitionBuilder("options", "生成后续4个分支剧情")
                .AddParameter("Options", PropertyDefinition.DefineArray(
                    PropertyDefinition.DefineObject(
                        new Dictionary<string, PropertyDefinition>()
                        {
                            { "Order", PropertyDefinition.DefineInteger("分支剧情的序号，按顺序分别为1、2、3、4") },
                            { "Name", PropertyDefinition.DefineString("分支剧情名称，长度不超过4个单词") },
                            { "Description", PropertyDefinition.DefineString("每条分支剧情的详细解释，长度不超过8个单词") }
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
                //修复一些换行符解析问题
                var jsonResult = completionResult.Choices.First().Message.FunctionCall.Arguments
                    .Replace((char)0x0D, ' ')
                    .Replace((char)0x0A, '\n');
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
    }
}
