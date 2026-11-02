FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/CatalogAPI/CatalogAPI.csproj -c Release -o /app/publish --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN mkdir /data && chown $APP_UID /data
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__Catalog="Data Source=/data/catalog.db"
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CatalogAPI.dll"]
