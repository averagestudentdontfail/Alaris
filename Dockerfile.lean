FROM mcr.microsoft.com/dotnet/runtime:8.0 as base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 as build
WORKDIR /src

# Create project file
RUN echo '<Project Sdk="Microsoft.NET.Sdk">' > Alaris.Lean.csproj \
    && echo '  <PropertyGroup>' >> Alaris.Lean.csproj \
    && echo '    <OutputType>Exe</OutputType>' >> Alaris.Lean.csproj \
    && echo '    <TargetFramework>net8.0</TargetFramework>' >> Alaris.Lean.csproj \
    && echo '    <ServerGarbageCollection>true</ServerGarbageCollection>' >> Alaris.Lean.csproj \
    && echo '  </PropertyGroup>' >> Alaris.Lean.csproj \
    && echo '  <ItemGroup>' >> Alaris.Lean.csproj \
    && echo '    <PackageReference Include="QuantConnect.Lean" Version="2.5.16909" />' >> Alaris.Lean.csproj \
    && echo '  </ItemGroup>' >> Alaris.Lean.csproj \
    && echo '</Project>' >> Alaris.Lean.csproj

RUN dotnet restore

# Copy source
COPY src/csharp/ ./
RUN dotnet build -c Release -o /app/build

FROM build as publish
RUN dotnet publish -c Release -o /app/publish

FROM base as final
WORKDIR /app
COPY --from=publish /app/publish .

RUN groupadd -r alaris \
    && useradd -r -g alaris alaris \
    && chown -R alaris:alaris /app

USER alaris

ENTRYPOINT ["dotnet", "Alaris.Lean.dll"]
