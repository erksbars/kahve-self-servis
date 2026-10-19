FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY services/api/Kahve.Api.csproj services/api/
RUN dotnet restore services/api/Kahve.Api.csproj
COPY services/api/ services/api/
RUN dotnet publish services/api/Kahve.Api.csproj --no-restore -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN mkdir -p /data/catalog-images && chown app:app /data/catalog-images
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Kahve.Api.dll"]
