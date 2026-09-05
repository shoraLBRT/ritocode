FROM mcr.microsoft.com/dotnet/sdk:10.0

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=1 \
    NUGET_PACKAGES=/opt/nuget/packages \
    HOME=/home/runner \
    TMPDIR=/tmp \
    NuGetAudit=false

RUN useradd --uid 10001 --create-home --shell /usr/sbin/nologin runner \
 && mkdir -p /opt/nuget/packages /opt/warm/src /work \
 && chown -R 10001:10001 /opt/nuget /opt/warm /work

COPY --chown=10001:10001 warm.csproj /opt/warm/warm.csproj

# Warm as the runner user, while the network is still up: this populates the package cache AND
# creates the dotnet CLI's first-run state under $HOME, which a read-only root cannot create later.
USER 10001:10001
RUN printf 'namespace Warm;\npublic static class W { public static int N => 1; }\n' > /opt/warm/src/W.cs \
 && cd /opt/warm && dotnet build -c Release && rm -rf /opt/warm/bin /opt/warm/obj

# Replace the config NuGet just wrote for itself with one that names no source at all.
USER root
COPY nuget.config /home/runner/.nuget/NuGet/NuGet.Config
RUN chown -R 10001:10001 /home/runner && chmod -R a+rX /opt/nuget /opt/warm

USER 10001:10001
WORKDIR /work
