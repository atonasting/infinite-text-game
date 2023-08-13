#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["InfiniteTextGame.Web/InfiniteTextGame.Web.csproj", "InfiniteTextGame.Web/"]
COPY ["InfiniteTextGame.Lib/InfiniteTextGame.Lib.csproj", "InfiniteTextGame.Lib/"]
COPY ["InfiniteTextGame.Models/InfiniteTextGame.Models.csproj", "InfiniteTextGame.Models/"]
RUN dotnet restore "InfiniteTextGame.Web/InfiniteTextGame.Web.csproj"
COPY . .
WORKDIR "/src/InfiniteTextGame.Web"
RUN dotnet build "InfiniteTextGame.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "InfiniteTextGame.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "InfiniteTextGame.Web.dll"]