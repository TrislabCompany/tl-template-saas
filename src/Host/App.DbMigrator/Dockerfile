# The migrator image, the init container of the api app. Stage 1 publishes the
# migrator with the .NET SDK. Stage 2 is the same chiseled runtime and non-root
# user as the api image. It applies every module's migrations and the seed data
# under a Postgres advisory lock, and the demo data where SEED_DEMO is true,
# then exits. The build context is the repository root.

FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
WORKDIR /repo
COPY global.json ./
COPY src/ src/
RUN dotnet restore src/Host/App.DbMigrator/App.DbMigrator.csproj
RUN dotnet publish src/Host/App.DbMigrator/App.DbMigrator.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-noble-chiseled@sha256:c89a6a61f494f20a5ae302990e814627a8d91b332fd4a8e7c1707c2d4da9555d
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV DOTNET_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "App.DbMigrator.dll"]
CMD ["apply", "--seed"]
