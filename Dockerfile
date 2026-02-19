FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/SmartAssistant.Gateway/*.csproj src/SmartAssistant.Gateway/
RUN dotnet restore src/SmartAssistant.Gateway/SmartAssistant.Gateway.csproj

COPY src/SmartAssistant.Gateway/ src/SmartAssistant.Gateway/
RUN dotnet publish src/SmartAssistant.Gateway/SmartAssistant.Gateway.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SmartAssistant.Gateway.dll"]
