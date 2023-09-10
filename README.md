# Infinite Text Game

一个基于 AI 内容生成的文字游戏

0.31 版，实现了手动和自动编写功能

# 功能列表

- 从原文段落提取写作风格，或者手动填写写作风格
- 基于写作风格，按章节编写文章
- 为每个章节提供后续剧情分支选项，只需点击既可无限延续故事
- 内置几个不同作者性格，可以自动选择分支并连续编写

# 运行

## 原生.net

1. 安装 asp.net runtime 6.0 或更高版本
1. 拥有 OpenAI 或 Azure 的 API Key
1. 在 InfiniteTextGame.Web 项目的 appsettings.json 中设置 Type 以及对应的 API Key 和其他配置项
1. 如果当前服务器无法直接访问 openai.com，可设置 WebProxy ，支持 HTTP 代理和 Socks5 代理

## 容器化

在 InfiniteTextGame.Web 项目根目录下编译 docker 镜像文件，或者直接 docker pull [atonasting/infinite-text-game](https://hub.docker.com/r/atonasting/infinite-text-game)

- 参考 docker-compose.yaml 的配置运行容器

# Todo

- 优化 prompt，提升文字质量和描写细致程度
- 增加剧情选项的丰富程度
- 增加自动重试和错误检测机制
