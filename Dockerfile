FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT=OrderGenerator
WORKDIR /source
COPY . .
RUN dotnet restore src/${PROJECT}/${PROJECT}.csproj
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release --no-restore -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ARG PROJECT=OrderGenerator
ENV APP_DLL=${PROJECT}.dll ASPNETCORE_HTTP_PORTS=8080 Fix__Store=/app/store
RUN mkdir -p /app/store && chown -R app:app /app
USER app
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
