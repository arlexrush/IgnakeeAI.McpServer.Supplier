FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5100
RUN apt-get update \
	&& apt-get install -y --no-install-recommends curl \
	&& rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/IgnakeeAI.McpServer.Supplier.Api/IgnakeeAI.McpServer.Supplier.Api.csproj", "src/IgnakeeAI.McpServer.Supplier.Api/"]
COPY ["src/IgnakeeAI.McpServer.Supplier.Application/IgnakeeAI.McpServer.Supplier.Application.csproj", "src/IgnakeeAI.McpServer.Supplier.Application/"]
COPY ["src/IgnakeeAI.McpServer.Supplier.Domain/IgnakeeAI.McpServer.Supplier.Domain.csproj", "src/IgnakeeAI.McpServer.Supplier.Domain/"]
COPY ["src/IgnakeeAI.McpServer.Supplier.Infrastructure/IgnakeeAI.McpServer.Supplier.Infrastructure.csproj", "src/IgnakeeAI.McpServer.Supplier.Infrastructure/"]
COPY ["src/IgnakeeAI.McpServer.Supplier.McpTools/IgnakeeAI.McpServer.Supplier.McpTools.csproj", "src/IgnakeeAI.McpServer.Supplier.McpTools/"]

RUN dotnet restore "src/IgnakeeAI.McpServer.Supplier.Api/IgnakeeAI.McpServer.Supplier.Api.csproj"

COPY . .
RUN dotnet publish "src/IgnakeeAI.McpServer.Supplier.Api/IgnakeeAI.McpServer.Supplier.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5100
ENTRYPOINT ["dotnet", "IgnakeeAI.McpServer.Supplier.Api.dll"]