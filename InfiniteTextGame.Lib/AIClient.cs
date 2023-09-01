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
        private readonly int _chapterLength = 1000;//默认每段长度（暂定）
        private readonly int _previousSummaryLength = 200;//默认前情提要长度（暂定）

        /// <summary>
        /// OpenAI构造函数
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="proxy"></param>
        public AIClient(string apiKey, string? defaultModel, string? proxy)
        {
            var httpClientFactory = new HttpClientFactoryWithProxy(proxy);

            _sdk = new OpenAIService(new OpenAiOptions()
            {
                ProviderType = ProviderType.OpenAi,
                ApiKey = apiKey,
                DefaultModelId = defaultModel ?? OpenAI.ObjectModels.Models.Gpt_3_5_Turbo_0613
            },
            httpClientFactory.CreateClient());
        }

        /// <summary>
        /// Azure构造函数
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="proxy"></param>
        public AIClient(string apiKey, string resourceName, string deploymentId, string? proxy)
        {
            var httpClientFactory = new HttpClientFactoryWithProxy(proxy);

            _sdk = new OpenAIService(new OpenAiOptions()
            {
                ProviderType = ProviderType.Azure,
                ApiKey = apiKey,
                ResourceName = resourceName,
                DeploymentId = deploymentId,
                DefaultModelId = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo_0613,
                ApiVersion = "2023-07-01-preview"
            },
            httpClientFactory.CreateClient());
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
                        completionResult.Choices.First().Message.FunctionCall.Arguments);
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
                ChatMessage.FromSystem("你是一位作家，你正在编写一部长篇故事。编写是逐个章节进行的。\n你能掌握故事迄今为止的发展、上个章节的内容、以及当前章节的发展方向。\n在这些内容基础上开始编写本章节内容，总结本章节之前的所有前情提要，以及下一个章节的提示"),
                ChatMessage.FromSystem($"你的文字风格有如下特征：\n{Style.KeyWords}"),
                ChatMessage.FromUser($"首先你来编写故事的第一个章节，需要交待故事的背景和主要人物，文字描写要细致。建议长度为{_chapterLength}个汉字"),
                ChatMessage.FromUser($"编写完第一个章节后。如果你认为后续剧情可以直接继续发展下去，就不必提供分支选项；如果你认为可以为读者提供分支选择，就设计四个不同剧情走向的分支选项，剧情会根据分支走向不同的方向。\n现在你可以开始编写了")
            };

            var chapterFunc = new FunctionDefinitionBuilder("chapter", "章节内容及后续分支选项")
                .AddParameter("StoryTitle", PropertyDefinition.DefineString($"故事标题，长度不超过20个汉字"))
                .AddParameter("Title", PropertyDefinition.DefineString($"本章节标题，长度不超过20个汉字"))
                .AddParameter("Content", PropertyDefinition.DefineString($"本章节内容，建议长度为{_chapterLength}个汉字"))
                .AddParameter("Options", PropertyDefinition.DefineArray(
                    PropertyDefinition.DefineObject(
                        new Dictionary<string, PropertyDefinition>()
                        {
                            { "Order", PropertyDefinition.DefineInteger("选项序号，按顺序分别为1、2、3、4") },
                            { "Name", PropertyDefinition.DefineString("选项名称，长度不超过10个汉字") },
                            { "Description", PropertyDefinition.DefineString("对选项的解释，长度不超过30个汉字") }
                        },
                        new List<string> { "Name", "Description" },
                        false, "剧情分支选项，一共4个", null)
                    ))
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
                firstChapter = JsonSerializer.Deserialize<StoryChapter>(jsonResult);
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
                Model = "GPT3.5",
                StylePrompt = Style.KeyWords,
                Chapters = new List<StoryChapter> { firstChapter }
            };

            firstChapter.CreateTime = DateTime.UtcNow;
            firstChapter.UseTime = useTime;
            firstChapter.PromptTokens = completionResult.Usage.PromptTokens;
            firstChapter.CompletionTokens = (int)completionResult.Usage.CompletionTokens;
            if (firstChapter.Options == null || firstChapter.Options.Count == 0 || firstChapter.Options.Count == 1)
            {
                firstChapter.Options = new List<StoryChapterOption> {
                    new StoryChapterOption()
                    {
                        IsContinue = true,
                        Name = "继续",
                        Order = 0
                    }
                };
            }

            Style.UseTimes++;

            return story;
        }

        /// <summary>
        /// 生成故事的下一章节
        /// </summary>
        /// <returns></returns>
        public async Task<StoryChapter> GenerateNextChapter(StoryChapter PreviousChapter, int OptionOrder = 0)
        {
            var story = PreviousChapter.Story;

            var messages = new List<ChatMessage> {
                ChatMessage.FromSystem("你是一位作家，你正在编写一部长篇故事。编写是逐个章节进行的。\n你能掌握故事迄今为止的发展、上个章节的内容、以及当前章节的发展方向。\n在这些内容基础上开始编写本章节内容，总结本章节之前的所有前情提要，以及下一个章节的提示。"),
                ChatMessage.FromSystem($"你的文字风格有如下特征：\n{story.StylePrompt}")
            };

            messages.Add(ChatMessage.FromUser($"接下来我会为你介绍一下之前的剧情和编写本章节的指导，你要根据这些来编写本章节内容。"));
            if (!string.IsNullOrEmpty(PreviousChapter.PreviousSummary))
            {
                messages.Add(ChatMessage.FromAssistant($"好的，请告诉我整个故事的前情提要。"));
                messages.Add(ChatMessage.FromUser(PreviousChapter.PreviousSummary));
            }
            messages.Add(ChatMessage.FromAssistant($"好的，请告诉我上一章节的内容。"));
            messages.Add(ChatMessage.FromUser(PreviousChapter.Content));
            if (OptionOrder > 0)
            {
                var option = PreviousChapter.Options.Single(o => o.Order == OptionOrder);
                messages.Add(ChatMessage.FromAssistant($"好的，我知道了上一章节的内容。请告诉我对本章节剧情的要求。"));
                messages.Add(ChatMessage.FromUser($"对本章节剧情的要求：{option.Name}，{option.Description}。\n章节建议长度为{_chapterLength}个汉字。"));
            }
            else
            {
                messages.Add(ChatMessage.FromAssistant($"好的，我知道了上一章节的内容。"));
                messages.Add(ChatMessage.FromUser($"请继续之前的剧情来编写本章节，建议长度为{_chapterLength}个汉字。"));
            }
            messages.Add(ChatMessage.FromAssistant($"好的，我已经完全了解应该如何编写本章节了。还有一个问题，本章节的后续剧情应该如何进行？"));
            messages.Add(ChatMessage.FromUser($"由你根据编写的剧情来判断。如果你认为后续剧情可以直接继续发展下去，就不必提供分支选项；如果你认为可以为读者提供分支选择，就为“Options”参数设计四个不同剧情走向的分支选项，剧情会随着选择分支的不同而走向不同的方向。\n现在你可以开始编写了。"));


            var chapterFunc = new FunctionDefinitionBuilder("chapter", "生成下一章节内容，包括后续分支选项")
                .AddParameter("Title", PropertyDefinition.DefineString($"本章节标题，长度不超过16个汉字"))
                .AddParameter("Content", PropertyDefinition.DefineString($"本章节内容，长度不少于{_chapterLength}个汉字。"))
                .AddParameter("PreviousSummary", PropertyDefinition.DefineString($"从前情提要和上一章节的内容总结出本章节的前情提要，长度不少于{_previousSummaryLength}个汉字。"))
                .AddParameter("Options", PropertyDefinition.DefineArray(
                    PropertyDefinition.DefineObject(
                        new Dictionary<string, PropertyDefinition>()
                        {
                            { "Order", PropertyDefinition.DefineInteger("选项序号，按顺序分别为1、2、3、4。") },
                            { "Name", PropertyDefinition.DefineString("选项名称，长度不超过8个汉字。") },
                            { "Description", PropertyDefinition.DefineString("对选项的解释，长度不超过30个汉字。") }
                        },
                        new List<string> { "Name", "Description" },
                        false, "剧情分支选项，一共4个", null)
                    ))
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
                chapter = JsonSerializer.Deserialize<StoryChapter>(jsonResult);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"deserialize chatgpt return json error:\n{ex.Message}\n{completionResult.Choices.First().Message.FunctionCall.Arguments}");
            }

            //为当前章节、前一章节和Story赋值
            chapter.CreateTime = DateTime.UtcNow;
            chapter.UseTime = useTime;
            chapter.PromptTokens = completionResult.Usage.PromptTokens;
            chapter.CompletionTokens = (int)completionResult.Usage.CompletionTokens;
            chapter.PreviousOptionOrder = OptionOrder;
            if (chapter.Options == null || chapter.Options.Count == 0 || chapter.Options.Count == 1)
            {
                chapter.Options = new List<StoryChapterOption> {
                        new StoryChapterOption()
                        {
                            Chapter = chapter,
                            IsContinue = true,
                            Name = "继续",
                            Order = 0
                        }
                    };
            }

            PreviousChapter.NextChapters ??= new List<StoryChapter>();
            PreviousChapter.NextChapters.Add(chapter);
            chapter.PreviousChapter = PreviousChapter;

            story.Chapters.Add(chapter);
            story.UpdateTime = DateTime.UtcNow;
            return chapter;
        }
    }
}
