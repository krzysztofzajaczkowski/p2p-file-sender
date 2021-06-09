FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build-env
WORKDIR /app
RUN apk update -yq 
RUN apk add nodejs
RUN apk add npm
COPY *sln ./
COPY ./FileExchanger ./FileExchanger
RUN dotnet restore ./FileExchanger/FileExchanger.csproj
RUN dotnet publish ./FileExchanger -c Release -o out



FROM mcr.microsoft.com/dotnet/aspnet:5.0
WORKDIR /home
COPY --from=build-env /app/out .
RUN apt-get update -yq
RUN apt-get install nodejs -yq
ENTRYPOINT dotnet FileExchanger.dll