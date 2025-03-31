# Используем официальный образ .NET SDK в качестве базового
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Устанавливаем необходимые зависимости для AOT-компиляции
`RUN apt-get update && apt-get install -y clang zlib1g-dev`: Эта строка устанавливает `clang` и библиотеку `zlib1g-dev`, необходимые для AOT-компиляции.  `apt-get` - это менеджер пакетов в Debian/Ubuntu.

# Копируем файл проекта и восстанавливаем зависимости
COPY PixelPick.csproj ./
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
ENTRYPOINT ["dotnet", "PixelPick.dll"]