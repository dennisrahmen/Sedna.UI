# The catalogue application and its MCP server.
#
# Build context is the REPOSITORY ROOT, not this directory: the app
# project-references the library and needs its wwwroot and its parts.
#
#     docker build -f src/Sedna.UI.Catalogue/Dockerfile -t sedna-ui-catalogue .
#
# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# global.json first, so an SDK image that does not satisfy the pinned feature band
# fails here rather than inside restore with a confusing message.
COPY global.json Sedna.UI.slnx ./
COPY src/Sedna.UI/Sedna.UI.csproj                     src/Sedna.UI/
COPY src/Sedna.UI.Catalogue/Sedna.UI.Catalogue.csproj src/Sedna.UI.Catalogue/
RUN dotnet restore src/Sedna.UI.Catalogue/Sedna.UI.Catalogue.csproj

COPY src/Sedna.UI/           src/Sedna.UI/
COPY src/Sedna.UI.Catalogue/ src/Sedna.UI.Catalogue/

# The MCP server serves these verbatim as get_integration_guide and as resources.
# Forgetting them is a compile error — the EmbeddedResource is missing — which is
# the right failure.
COPY docs/ docs/

# Baked in, not read from a runtime `git` call: .dockerignore excludes .git on
# purpose. The MCP version envelope reports this.
ARG SOURCE_COMMIT=unknown
RUN dotnet publish src/Sedna.UI.Catalogue/Sedna.UI.Catalogue.csproj \
        -c Release -o /app --no-restore -p:SourceRevisionId=$SOURCE_COMMIT

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Every .NET 8+ image ships a non-root `app` user behind this variable.
USER $APP_UID

# The .NET 8+ default, and what a Railway domain's target port expects. Railway
# also injects PORT; set the service variable ASPNETCORE_HTTP_PORTS=${PORT} there
# and it wins. This ENV is what makes `docker run -p 8080:8080` work locally with
# no arguments.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Sedna.UI.Catalogue.dll"]
