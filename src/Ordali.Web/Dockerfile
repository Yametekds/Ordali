FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props .editorconfig ./
COPY src/Ordali.Client/Ordali.Client.csproj src/Ordali.Client/
COPY src/Ordali.Ui/Ordali.Ui.csproj src/Ordali.Ui/
COPY src/Ordali.Web/Ordali.Web.csproj src/Ordali.Web/
RUN dotnet restore src/Ordali.Web/Ordali.Web.csproj

COPY src/Ordali.Client/ src/Ordali.Client/
COPY src/Ordali.Ui/ src/Ordali.Ui/
COPY src/Ordali.Web/ src/Ordali.Web/
RUN dotnet publish src/Ordali.Web/Ordali.Web.csproj -c Release -o /app --no-restore

FROM nginx:1.27-alpine
COPY src/Ordali.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY src/Ordali.Web/docker-entrypoint.sh /docker-entrypoint.d/40-api-url.sh
COPY --from=build /app/wwwroot /usr/share/nginx/html
RUN chmod +x /docker-entrypoint.d/40-api-url.sh
EXPOSE 80
