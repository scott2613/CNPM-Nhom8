FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore AudioGuide.API/AudioGuide.API.csproj --locked-mode && dotnet restore tools/Database/Database.csproj --locked-mode
RUN dotnet publish AudioGuide.API/AudioGuide.API.csproj -c Release --no-restore -o /out/api && dotnet publish tools/Database/Database.csproj -c Release --no-restore -o /out/database
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out/api ./
COPY --from=build /out/database ./database/
COPY sql ./sql/
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","AudioGuide.API.dll"]
