# Etapa 1: Compilación y publicación con .NET 8 SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restauramos primero los .csproj para aprovechar la caché de Docker
COPY OrderFlow.Contracts/OrderFlow.Contracts.csproj OrderFlow.Contracts/
COPY Orders.Api/Orders.Api.csproj Orders.Api/
RUN dotnet restore Orders.Api/Orders.Api.csproj

COPY OrderFlow.Contracts/ OrderFlow.Contracts/
COPY Orders.Api/ Orders.Api/
RUN dotnet publish Orders.Api/Orders.Api.csproj -c Release -o /app/publish --no-restore

# Etapa 2: Imagen final liviana solo con ASP.NET Core Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Orders.Api.dll"]