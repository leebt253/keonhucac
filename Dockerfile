FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WorldCupBetting.Web/WorldCupBetting.Web.csproj WorldCupBetting.Web/
RUN dotnet restore WorldCupBetting.Web/WorldCupBetting.Web.csproj

COPY . .
RUN dotnet publish WorldCupBetting.Web/WorldCupBetting.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:10000
ENV ASPNETCORE_HTTP_PORTS=10000
ENV ConnectionStrings__DefaultConnection="Data Source=/var/data/worldcup.db"

RUN mkdir -p /var/data

COPY --from=build /app/publish .

EXPOSE 10000
ENTRYPOINT ["dotnet", "WorldCupBetting.Web.dll"]
