# The official Node 6.6 image has vulnerabilities, so we build our own version
# to fetch the latest stretch release with the required fixes.
# https://github.com/nodejs/docker-node.git, commit ID 9cd8d4c47e9e695bb38d98a32c4dd45dfa981962.
FROM oryx-node-run-base-stretch

ENV NPM_CONFIG_LOGLEVEL info
ENV NODE_VERSION 6.6.0

ARG IMAGES_DIR=/tmp/oryx/images
ARG BUILD_DIR=/tmp/oryx/build
RUN set -ex \
    && . ${BUILD_DIR}/__sdkStorageConstants.sh \
    && ${IMAGES_DIR}/installPlatform.sh -p nodejs -v $NODE_VERSION -b /usr/local --use-specified-dir \
    && ln -s /usr/local/bin/node /usr/local/bin/nodejs
RUN ${IMAGES_DIR}/runtime/node/installDependencies.sh
RUN rm -rf /tmp/oryx

CMD [ "node" ]
