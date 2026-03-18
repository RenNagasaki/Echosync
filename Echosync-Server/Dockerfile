FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY Echosync-Data/Echosync-Data.csproj Echosync-Data/
COPY Echosync-Server/Echosync-Server.csproj Echosync-Server/
RUN dotnet restore Echosync-Server/Echosync-Server.csproj

COPY Echosync-Data/ Echosync-Data/
COPY Echosync-Server/ Echosync-Server/
RUN dotnet publish Echosync-Server/Echosync-Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0-preview
WORKDIR /app
COPY --from=build /app .

EXPOSE 2053

ENTRYPOINT ["dotnet", "Echosync-Server.dll"]
