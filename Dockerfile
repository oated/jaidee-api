FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["JaiDee.API/JaiDee.API.csproj", "JaiDee.API/"]
COPY ["JaiDee.Application/JaiDee.Application.csproj", "JaiDee.Application/"]
COPY ["JaiDee.Domain/JaiDee.Domain.csproj", "JaiDee.Domain/"]
COPY ["JaiDee.Infrastructure/JaiDee.Infrastructure.csproj", "JaiDee.Infrastructure/"]
RUN dotnet restore "JaiDee.API/JaiDee.API.csproj"

COPY . .
RUN dotnet publish "JaiDee.API/JaiDee.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "JaiDee.API.dll"]
