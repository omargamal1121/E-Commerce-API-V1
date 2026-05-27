FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY ["PresentationLayer/E-Commerce.API.csproj", "PresentationLayer/"]
COPY ["ApplicationLayer/E-Commerce.Application.csproj", "ApplicationLayer/"]
COPY ["DomainLayer/E-Commerce.Domain.csproj", "DomainLayer/"]
COPY ["InfrastructureLayer/E-Commerce.Infrastructure.csproj", "InfrastructureLayer/"]

RUN dotnet restore "./PresentationLayer/E-Commerce.API.csproj"

COPY . .

WORKDIR "/src/PresentationLayer"

RUN dotnet build "E-Commerce.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "E-Commerce.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

ENV ASPNETCORE_URLS="https://+:8081;http://+:8080"

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "E-Commerce.API.dll"]