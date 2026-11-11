FROM node:22-alpine AS ui-build
WORKDIR /src/HongBeautyPOSUI
COPY HongBeautyPOSUI/package.json HongBeautyPOSUI/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY HongBeautyPOSUI/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY HongBeautyPOSAPI/ ./HongBeautyPOSAPI/
RUN dotnet restore HongBeautyPOSAPI/HongBeautyPOS.slnx
RUN dotnet publish HongBeautyPOSAPI/src/HongBeautyPOS.Api/HongBeautyPOS.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=api-build /app/publish ./
COPY --from=ui-build /src/HongBeautyPOSUI/dist/HongBeautyPOSUI/browser/ ./wwwroot/
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD curl --fail --silent http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "HongBeautyPOS.Api.dll"]
