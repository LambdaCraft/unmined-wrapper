FROM mcr.microsoft.com/dotnet/core/sdk:3.0.101-alpine3.10 as build

ENV UNMINED_DOWNLOAD=https://unmined.net/download/298/

WORKDIR /app

COPY . /app

RUN apk add curl && \
	curl $UNMINED_DOWNLOAD > deps.zip && \
	mkdir deps && \
	unzip deps.zip -jd ./deps && \
	echo '{ "packages": "./deps" }' > global.json && \
	dotnet build CLIWrapper.csproj -o /bin/cli 

################################

FROM mcr.microsoft.com/dotnet/core/runtime:3.0.1-alpine3.10

WORKDIR /app

COPY --from=build /app/deps/*.dll ./
COPY --from=build /app/deps/unmined-cli .
COPY --from=build /app/Metapacks ./Metapacks
COPY --from=build /bin/cli/CLIWrapper* ./
COPY ./template /template
COPY ./run.sh .

RUN chmod +x run.sh

ENV WORLD=/world
ENV OUTPUT=/generated
ENV WORLD_NAME=tech
ENV FORCE_RE_GEN=true

CMD [ "sh", "-c", "./run.sh"]