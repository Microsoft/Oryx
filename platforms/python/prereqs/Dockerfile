# Install Python build prerequisites
ARG DEBIAN_FLAVOR
FROM buildpack-deps:${DEBIAN_FLAVOR}
ARG DEBIAN_FLAVOR
ENV DEBIAN_FLAVOR=$DEBIAN_FLAVOR
COPY build/__pythonVersions.sh /tmp/

COPY platforms/python/prereqs/build.sh /tmp/
COPY images/receiveGpgKeys.sh /tmp/receiveGpgKeys.sh
RUN chmod +x /tmp/receiveGpgKeys.sh
RUN chmod +x /tmp/build.sh && \
	apt-get update && \
	DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
		build-essential \ 
		tk-dev \
		uuid-dev \
		libgeos-dev
