FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props .editorconfig ./
COPY src/Ordali.Api/Ordali.Api.csproj src/Ordali.Api/
RUN dotnet restore src/Ordali.Api/Ordali.Api.csproj

COPY src/ src/
RUN dotnet publish src/Ordali.Api/Ordali.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Ordali.Api.dll"]
