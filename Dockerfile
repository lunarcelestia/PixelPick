# Используем официальный образ .NET SDK в качестве базового
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Копируем файл проекта и восстанавливаем зависимости
COPY *.csproj ./
RUN dotnet restore

# Копируем весь код приложения
COPY . ./

# Публикуем приложение
RUN dotnet publish -c Release -o out

# Создаем новый образ на основе ASP.NET Core Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Указываем точку входа для приложения
ENTRYPOINT ["dotnet", "PixelPick.dll"
