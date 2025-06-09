FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
ARG APP_UID=1000
ENV APP_UID=$APP_UID
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["dynamics-mcp.csproj", "./"]
RUN dotnet restore "dynamics-mcp.csproj"
COPY . .
RUN dotnet build "dynamics-mcp.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "dynamics-mcp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "dynamics-mcp.dll"]
