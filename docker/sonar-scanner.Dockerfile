# syntax=docker/dockerfile:1

# =============================================================================
# Runs dotnet-sonarscanner against the solution.
#
# It needs both toolchains in one image: the .NET SDK to build and collect coverage,
# and a JRE because the SonarScanner engine itself is Java. Neither official image
# has both, which is why this small one exists instead of a plain `image:` entry in
# docker-compose.yml.
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
    && apt-get install --yes --no-install-recommends default-jre-headless \
    && rm -rf /var/lib/apt/lists/*

# Installed globally at build time so a scan does not spend its first minute
# downloading the tool.
RUN dotnet tool install --global dotnet-sonarscanner
ENV PATH="${PATH}:/root/.dotnet/tools"

WORKDIR /src

COPY docker/sonar-scan.sh /usr/local/bin/sonar-scan
RUN chmod +x /usr/local/bin/sonar-scan

ENTRYPOINT ["/usr/local/bin/sonar-scan"]
