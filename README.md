# Infinite Text Game
[![.NET](https://github.com/atonasting/infinite-text-game/actions/workflows/dotnet.yml/badge.svg)](https://github.com/atonasting/infinite-text-game/actions/workflows/dotnet.yml)
![Docker Image Version (latest semver)](https://img.shields.io/docker/v/atonasting/infinite-text-game?logo=docker&color=blue&link=https%3A%2F%2Fhub.docker.com%2Fr%2Fatonasting%2Finfinite-text-game)

一个基于 AI 内容生成的文字游戏

# 版本功能列表

- 设计写作风格
- 基于写作风格，按章节编写文章
- 为每个章节提供后续剧情分支选项，只需点击既可无限延续故事
- 内置几个不同作者性格，可以自动选择分支并连续编写

# 运行

## .net原生

1. 安装 asp.net runtime 6.0 或更高版本
1. 拥有 OpenAI 或 Azure 的 API Key
1. git clone
1. 修改 InfiniteTextGame.Web 项目的 appsettings.json
    1. 修改 Type 为 OpenAI 或 Azure，并设置对应的 API Key 和其他选项
    1. 如果本地服务器无法直接访问 openai.com，设置 WebProxy ，支持 HTTP 代理和 Socks5 代理
1. dotnet run

## 容器化

1. 获取镜像
    1. 在 InfiniteTextGame.Web 项目根目录下编译 docker 镜像文件。
    1. 或者直接 ```docker pull atonasting/infinite-text-game```。
1. 运行容器：建议使用docker compose运行。

docker-compose.yaml样例如下：

#### OpenAI

```
version: '3'

services:
    infinite-text-game:
        image: atonasting/infinite-text-game:latest
        container_name: infinite-text-game
        environment:
            - TZ=Asia/Shanghai
            - Type=OpenAI 
            - WebProxy=xxx # 代理服务器，部署在国内时使用。格式：http://ip:port 或 socks5://ip:port
            - OpenAIApiKey=xxx # api key 
            - DefaultModel=xxx # 默认模型名称，如gpt-3.5-turbo-16k-0613或gpt-4-0613等等，默认为gpt-3.5-turbo-0613
        ports:
            - '9000:80'
```

#### Azure
```
version: '3'

services:
    infinite-text-game:
        image: atonasting/infinite-text-game:latest
        container_name: infinite-text-game
        environment:
            - TZ=Asia/Shanghai
            - Type=Azure
            - AzureApiKey=xxx # api key 
            - ResourceName=xxx # 资源名称，用于生成终结点地址：https://{ResourceName}.openai.azure.com/
            - DeploymentId=xxx # 在Azure OpenAI studio部署的模型id
        ports:
            - '9000:80'
```

# Todo

- 优化prompt，提升文字质量和描写细致程度，尽量减少“ChatGPT味”
- 增加剧情选项的丰富程度
- 增加对话选项模式，体现类似文字AVG的风格
- 测试主视角冒险游戏风格是否可行
- 改进自动重试和错误检测机制
