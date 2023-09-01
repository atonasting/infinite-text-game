# Infinite Text Game

一个基于 AI 内容生成的文字游戏

0.2 版，实现了最基础的功能

# 已有功能

- 从原文段落提取写作风格，或者手动填写写作风格
- 基于写作风格编写文章
- 根据情节走向，每个章节可以直接继续或提供分支选项，只需点击即可不断无限编写剧情

# 运行

## 原生.net

1. 安装 asp.net runtime 6.0 或更高版本
1. 拥有 OpenAI 或 Azure 的 API Key
1. 在 InfiniteTextGame.Web 项目的 appsettings.json 中设置 Type 以及对应的 API Key 和其他配置项
1. 如果当前服务器无法直接访问 openai.com，可设置 WebProxy ，支持 HTTP 代理和 Socks5 代理

## 容器化

1. 在 InfiniteTextGame.Web 项目根目录下编译 docker 镜像文件
1. 参考 docker-compose.yaml 的内容运行容器

# Todo

- 优化 prompt，提升文字质量
- 增加剧情选项的丰富程度
- 自动编写引擎