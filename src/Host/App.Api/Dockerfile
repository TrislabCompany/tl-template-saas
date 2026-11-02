# The api image. Stage 1 publishes the API with the .NET SDK. Stage 2 is the
# chiseled ASP.NET runtime: no shell, no package manager, a non-root user, and
# the API on port 8080. Both base images are pinned by digest; Renovate bumps
# them from roadmap phase 12 on. The build context is the repository root.

FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
WORKDIR /repo
# The whole of src/ but the tests (.dockerignore): Directory.Build.props and
# Directory.Packages.props hold the versions, and the API references every module.
COPY global.json ./
COPY src/ src/
RUN dotnet restore src/Host/App.Api/App.Api.csproj
RUN dotnet publish src/Host/App.Api/App.Api.csproj --configuration Release --no-restore --output /app \
    && mkdir -p /uploads

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-noble-chiseled@sha256:c89a6a61f494f20a5ae302990e814627a8d91b332fd4a8e7c1707c2d4da9555d
WORKDIR /app
COPY --from=build /app .
# Local disk storage, used when the stamp has no storage service: the only
# folder the non-root user may write. Its files are lost on restart.
COPY --from=build --chown=$APP_UID:$APP_UID /uploads ./wwwroot/uploads
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "App.Api.dll"]
