FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY .editorconfig Directory.Build.props Directory.Packages.props global.json ./
COPY src/CidadeViva.Api/CidadeViva.Api.csproj src/CidadeViva.Api/
COPY src/CidadeViva.Application/CidadeViva.Application.csproj src/CidadeViva.Application/
COPY src/CidadeViva.Domain/CidadeViva.Domain.csproj src/CidadeViva.Domain/
COPY src/CidadeViva.Infrastructure/CidadeViva.Infrastructure.csproj src/CidadeViva.Infrastructure/
RUN dotnet restore src/CidadeViva.Api
COPY src/ src/
RUN dotnet publish src/CidadeViva.Api -c Release --no-restore -o /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* \
    && mkdir /app/.keys && chown app:app /app/.keys
COPY --from=build /publish/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "CidadeViva.Api.dll"]
