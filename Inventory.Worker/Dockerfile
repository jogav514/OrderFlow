# Etapa 1: Compilación y publicación con .NET 8 SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OrderFlow.Contracts/OrderFlow.Contracts.csproj OrderFlow.Contracts/
COPY Inventory.Worker/Inventory.Worker.csproj Inventory.Worker/
RUN dotnet restore Inventory.Worker/Inventory.Worker.csproj

COPY OrderFlow.Contracts/ OrderFlow.Contracts/
COPY Inventory.Worker/ Inventory.Worker/
RUN dotnet publish Inventory.Worker/Inventory.Worker.csproj -c Release -o /app/publish --no-restore

# Etapa 2: Imagen final liviana solo con .NET Runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Inventory.Worker.dll"]