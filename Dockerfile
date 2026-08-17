# ============================================================
# BUILD
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY ["BizPulse.AI.POC/BizPulse.AI.POC.csproj", "BizPulse.AI.POC/"]

RUN dotnet restore "BizPulse.AI.POC/BizPulse.AI.POC.csproj"

COPY . .

WORKDIR "/src/BizPulse.AI.POC"

RUN dotnet publish "BizPulse.AI.POC.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


# ============================================================
# RUNTIME
# ============================================================

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "BizPulse.AI.POC.dll"]